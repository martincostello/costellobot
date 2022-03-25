// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

//// See https://github.com/octokit/webhooks.net/blob/0818566a5fea94f47a24d5f721ff07cae11f9012/src/Octokit.Webhooks.AspNetCore/GitHubWebhookExtensions.cs

#pragma warning disable CA1848

using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public static class GitHubWebhookExtensions
{
    public static void MapGitHubWebhooks(this IEndpointRouteBuilder endpoints, string path = "/github-webhook", string secret = null!) =>
        endpoints.MapPost(path, async context =>
        {
            var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Octokit.Webhooks.AspNetCore");

            // Verify content type
            if (!VerifyContentType(context, MediaTypeNames.Application.Json))
            {
                logger.LogError("GitHub event does not have the correct content type.");
                return;
            }

            // Get body
            var body = await GetBodyAsync(context).ConfigureAwait(false);

            // Verify signature
            if (!await VerifySignatureAsync(context, secret, body).ConfigureAwait(false))
            {
                logger.LogError("GitHub event failed signature validation.");
                return;
            }

            // Process body
            try
            {
                var service = context.RequestServices.GetRequiredService<WebhookEventProcessor>();
                await service.ProcessWebhookAsync(context.Request.Headers, body)
                    .ConfigureAwait(false);
                context.Response.StatusCode = 200;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception processing GitHub event.");
                context.Response.StatusCode = 500;
            }
        });

    private static bool VerifyContentType(HttpContext context, string expectedContentType)
    {
        var contentType = new ContentType(context.Request.ContentType!);

        if (contentType.MediaType != expectedContentType)
        {
            context.Response.StatusCode = 400;
            return false;
        }

        return true;
    }

    private static async Task<string> GetBodyAsync(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private static async Task<bool> VerifySignatureAsync(HttpContext context, string secret, string body)
    {
        context.Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureSha256);

        var isSigned = signatureSha256.Count > 0;
        var isSignatureExpected = !string.IsNullOrEmpty(secret);

        if (!isSigned && !isSignatureExpected)
        {
            // Nothing to do.
            return true;
        }

        if (!isSigned && isSignatureExpected)
        {
            context.Response.StatusCode = 400;
            return false;
        }

        if (isSigned && !isSignatureExpected)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Payload includes a secret, so the webhook receiver must configure a secret.")
                .ConfigureAwait(false);
            return false;
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        var hash = HMACSHA256.HashData(keyBytes, bodyBytes);
        var hashHex = Convert.ToHexString(hash);

#pragma warning disable CA1308
        var expectedHeader = $"sha256={hashHex.ToLowerInvariant()}";
#pragma warning restore CA1308

        if (signatureSha256.ToString() != expectedHeader)
        {
            context.Response.StatusCode = 400;
            return false;
        }

        return true;
    }
}
