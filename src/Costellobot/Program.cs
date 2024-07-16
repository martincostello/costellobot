// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using MartinCostello.Costellobot;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

if (builder.Configuration["ConnectionStrings:AzureKeyVault"] is { Length: > 0 })
{
    builder.Configuration.AddAzureKeyVaultSecrets("AzureKeyVault");
}

builder.AddAzureServiceBusClient("AzureServiceBus");

if (builder.Configuration["ConnectionStrings:AzureBlobStorage"] is { Length: > 0 })
{
    builder.AddAzureBlobClient("AzureBlobStorage");
}

builder.Services.AddAntiforgery();
builder.Services.AddGitHub(builder.Configuration);
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

app.Run();

namespace MartinCostello.Costellobot
{
    public partial class Program
    {
        // Expose the Program class for use with WebApplicationFactory<T>
    }
}
