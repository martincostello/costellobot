// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class CustomHttpHeadersMiddleware
{
    private static readonly string ContentSecurityPolicy = string.Join(
        ';',
        new[]
        {
            "default-src 'self'",
            "script-src 'self' cdn.jsdelivr.net cdnjs.cloudflare.com",
            "script-src-elem 'self' cdn.jsdelivr.net cdnjs.cloudflare.com",
            "style-src 'self' cdn.jsdelivr.net cdnjs.cloudflare.com",
            "style-src-elem 'self' cdn.jsdelivr.net cdnjs.cloudflare.com",
            "img-src 'self'",
            "font-src 'self' cdnjs.cloudflare.com",
            "connect-src 'none'",
            "media-src 'none'",
            "object-src 'none'",
            "child-src 'none'",
            "frame-ancestors 'none'",
            "form-action 'none'",
            "block-all-mixed-content",
            "base-uri 'self'",
            "manifest-src 'self'",
            "upgrade-insecure-requests",
        });

    private readonly RequestDelegate _next;

    public CustomHttpHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext context, IWebHostEnvironment environment)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Remove("Server");
            context.Response.Headers.Remove("X-Powered-By");

            if (environment.IsProduction())
            {
                context.Response.Headers.Add("Content-Security-Policy", ContentSecurityPolicy);
            }

            if (context.Request.IsHttps)
            {
                context.Response.Headers.Add("Expect-CT", "max-age=1800");
            }

            context.Response.Headers.Add("Feature-Policy", "accelerometer 'none'; camera 'none'; geolocation 'none'; gyroscope 'none'; magnetometer 'none'; microphone 'none'; payment 'none'; usb 'none'");
            context.Response.Headers.Add("Permissions-Policy", "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
            context.Response.Headers.Add("Referrer-Policy", "no-referrer-when-downgrade");
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Download-Options", "noopen");

            if (!context.Response.Headers.ContainsKey("X-Frame-Options"))
            {
                context.Response.Headers.Add("X-Frame-Options", "DENY");
            }

            context.Response.Headers.Add("X-Request-Id", context.TraceIdentifier);
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
