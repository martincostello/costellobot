// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using AspNet.Security.OAuth.GitHub;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace MartinCostello.Costellobot;

public sealed class CustomHttpHeadersMiddleware(RequestDelegate next)
{
    private static readonly string BaseContentSecurityPolicy = string.Join(
        ';',
        "default-src 'self'",
        "script-src 'self' cdnjs.cloudflare.com",
        "script-src-elem 'self' cdnjs.cloudflare.com",
        "style-src 'self' cdnjs.cloudflare.com use.fontawesome.com",
        "style-src-elem 'self' cdnjs.cloudflare.com use.fontawesome.com",
        "img-src 'self' data: avatars.githubusercontent.com cdn.martincostello.com",
        "font-src 'self' cdnjs.cloudflare.com use.fontawesome.com",
        "media-src 'none'",
        "object-src 'none'",
        "child-src 'none'",
        "frame-ancestors 'none'",
        "block-all-mixed-content",
        "base-uri 'self'",
        "manifest-src 'self'",
        "upgrade-insecure-requests",
        "connect-src 'self' cdnjs.cloudflare.com");

    private volatile string? _contentSecurityPolicy;

    public Task Invoke(
        HttpContext context,
        IHostEnvironment environment,
        IOptions<GitHubAuthenticationOptions> gitHubOptions,
        IOptions<SiteOptions> siteOptions)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Remove(HeaderNames.Server);
            context.Response.Headers.Remove(HeaderNames.XPoweredBy);

            if (environment.IsProduction())
            {
                context.Response.Headers.ContentSecurityPolicy = GetContentSecurityPolicy(
                    gitHubOptions.Value.AuthorizationEndpoint,
                    siteOptions.Value.TelemetryCollectorUrl);
            }

            context.Response.Headers["Cross-Origin-Embedder-Policy"] = "unsafe-none";
            context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
            context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";

            if (context.Request.IsHttps)
            {
                context.Response.Headers["Expect-CT"] = "max-age=1800";
            }

            context.Response.Headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
            context.Response.Headers["Referrer-Policy"] = "no-referrer-when-downgrade";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["X-Download-Options"] = "noopen";

            if (!context.Response.Headers.ContainsKey(HeaderNames.XFrameOptions))
            {
                context.Response.Headers.XFrameOptions = "DENY";
            }

            context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
            context.Response.Headers.XXSSProtection = "1; mode=block";

            return Task.CompletedTask;
        });

        return next(context);
    }

    private static string ParseGitHubHost(string gitHubAuthorizationEndpoint)
    {
        if (Uri.TryCreate(gitHubAuthorizationEndpoint, UriKind.Absolute, out Uri? gitHubHost))
        {
            return gitHubHost.Host;
        }

        return "github.com";
    }

    private static string? ParseTelemetryCollector(string telemetryCollectorEndpoint)
    {
        if (Uri.TryCreate(telemetryCollectorEndpoint, UriKind.Absolute, out Uri? collector))
        {
            return collector.Host;
        }

        return null;
    }

    private string GetContentSecurityPolicy(
        string gitHubAuthorizationEndpoint,
        string telemetryCollectorEndpoint)
    {
        if (_contentSecurityPolicy is null)
        {
            var builder = new StringBuilder(BaseContentSecurityPolicy);

            if (ParseTelemetryCollector(telemetryCollectorEndpoint) is { Length: > 0 } collector)
            {
                builder.Append(' ')
                       .Append(collector);
            }

            builder.Append(";form-action 'self' ")
                   .Append(ParseGitHubHost(gitHubAuthorizationEndpoint));

            _contentSecurityPolicy = builder.ToString();
        }

        return _contentSecurityPolicy;
    }
}
