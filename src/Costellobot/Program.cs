// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MartinCostello.Costellobot;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureApplication();

builder.AddAzureServiceBusClient("AzureServiceBus");

builder.Services.AddAntiforgery();
builder.Services.AddGitHub(builder.Configuration, builder.Environment);
builder.Services.AddHsts((options) => options.MaxAge = TimeSpan.FromDays(180));
builder.Services.AddResponseCaching();
builder.Services.AddTelemetry(builder.Environment);

builder.Services.AddResponseCompression((options) =>
{
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<ClientLogQueue>();
builder.Services.AddHostedService<ClientLogBroadcastService>();

builder.Logging.AddTelemetry();
builder.Logging.AddSignalR();

builder.Services.ConfigureHttpJsonOptions((options) =>
{
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.Configure<StaticFileOptions>((options) =>
{
    options.OnPrepareResponse = (context) =>
    {
        var maxAge = TimeSpan.FromDays(7);

        if (context.File.Exists)
        {
            string? extension = Path.GetExtension(context.File.PhysicalPath);

            // These files are served with a content hash in the URL so can be cached for longer
            bool isScriptOrStyle =
                string.Equals(extension, ".css", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase);

            if (isScriptOrStyle)
            {
                maxAge = TimeSpan.FromDays(365);
            }
        }

        context.Context.Response.GetTypedHeaders().CacheControl = new() { MaxAge = maxAge };
    };
});

builder.Services.Configure<BrotliCompressionProviderOptions>((p) => p.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>((p) => p.Level = CompressionLevel.Fastest);

if (string.Equals(builder.Configuration["CODESPACES"], bool.TrueString, StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<ForwardedHeadersOptions>(
        (options) => options.ForwardedHeaders |= ForwardedHeaders.XForwardedHost);
}

builder.WebHost.ConfigureKestrel((p) => p.AddServerHeader = false);

var app = builder.Build();

// Give the webhook queue a chance to drain before the application stops
app.Lifetime.ApplicationStopping.Register(() => Thread.Sleep(TimeSpan.FromSeconds(10)));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseMiddleware<CustomHttpHeadersMiddleware>();

app.UseStatusCodePagesWithReExecute("/error", "?id={0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();

    if (!string.Equals(app.Configuration["ForwardedHeaders_Enabled"], bool.TrueString, StringComparison.OrdinalIgnoreCase))
    {
        app.UseHttpsRedirection();
    }
}

app.UseResponseCompression();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapAuthenticationRoutes();
app.MapApiRoutes(app.Configuration);
app.MapAdminRoutes();

app.MapPost("send", async (ServiceBusClient client, IOptions<WebhookOptions> options, CancellationToken cancellationToken) =>
{
    var payload = new GitHubMessage()
    {
        Headers = [],
        Body =
            /*lang=json,strict*/
            """
            {
            }
            """,
    };

    var sender = client.CreateSender(options.Value.QueueName);

    using var batch = await sender.CreateMessageBatchAsync(cancellationToken);

    var json = JsonSerializer.SerializeToUtf8Bytes(payload, MessagingJsonSerializerContext.Default.GitHubMessage);
    var message = new ServiceBusMessage(json)
    {
        ApplicationProperties = { ["publisher"] = $"costellobot/{GitMetadata.Version}" },
        ContentType = GitHubMessage.ContentType,
        CorrelationId = Activity.Current?.Id,
        MessageId = "57067d40-3924-11ef-8ab8-48a7429f1fa3",
        Subject = GitHubMessage.Subject,
    };

    if (!batch.TryAddMessage(message))
    {
        throw new InvalidOperationException("Payload is too large to send.");
    }

    await sender.SendMessagesAsync(batch, cancellationToken);
});

app.MapPost("receive", async (ServiceBusClient client, IOptions<WebhookOptions> options, CancellationToken cancellationToken) =>
{
    await using var processor = client.CreateProcessor(options.Value.QueueName);

    processor.ProcessMessageAsync += async (args) =>
    {
        if (args.Message.ContentType != "application/json" ||
            args.Message.Subject != "github-webhook")
        {
            throw new InvalidOperationException("Invalid message content type and/or subject.");
        }

        using var document = args.Message.Body.ToObjectFromJson<JsonDocument>();
        await args.CompleteMessageAsync(args.Message, cancellationToken);
    };

    processor.ProcessErrorAsync += async (args) =>
    {
        await Task.CompletedTask;
    };

    await processor.StartProcessingAsync(cancellationToken);

    await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);

    await processor.StopProcessingAsync(CancellationToken.None);
});

app.Run();

namespace MartinCostello.Costellobot
{
    public partial class Program
    {
        // Expose the Program class for use with WebApplicationFactory<T>
    }
}
