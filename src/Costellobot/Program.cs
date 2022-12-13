// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable CA1852

using System.IO.Compression;
using MartinCostello.Costellobot;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Octokit.Webhooks.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureApplication();

builder.Services.AddGitHub(builder.Configuration, builder.Environment);
builder.Services.AddRazorPages();
builder.Services.AddResponseCaching();

builder.Services.AddResponseCompression((options) =>
{
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<ClientLogQueue>();
builder.Services.AddHostedService<ClientLogBroadcastService>();

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
        options => options.ForwardedHeaders |= ForwardedHeaders.XForwardedHost);
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
    app.UseHttpsRedirection();
}

app.UseResponseCompression();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthenticationRoutes();
app.MapGitHubWebhooks("/github-webhook", app.Configuration["GitHub:WebhookSecret"] ?? string.Empty);

app.MapGet("/version", () => new
{
    branch = GitMetadata.Branch,
    build = GitMetadata.BuildId,
    commit = GitMetadata.Commit,
    version = GitMetadata.Version,
    _links = new
    {
        self = new { href = "https://costellobot.martincostello.com" },
        repo = new { href = "https://github.com/martincostello/costellobot" },
        branch = new { href = $"https://github.com/martincostello/costellobot/tree/{GitMetadata.Branch}" },
        commit = new { href = $"https://github.com/martincostello/costellobot/commit/{GitMetadata.Commit}" },
        deploy = new { href = $"https://github.com/martincostello/costellobot/actions/runs/{GitMetadata.BuildId}" },
    },
}).AllowAnonymous();

app.MapRazorPages();

app.MapHub<GitHubWebhookHub>("/admin/git-hub");

app.Run();

namespace MartinCostello.Costellobot
{
    public partial class Program
    {
        // Expose the Program class for use with WebApplicationFactory<T>
    }
}
