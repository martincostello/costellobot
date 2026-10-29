// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubEventProcessor(
    GitHubEventHandler handler,
    IHubContext<GitHubWebhookHub> hub,
    GitHubWebhookHubConnections connections,
    CostellobotMetrics metrics,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<GitHubEventProcessor> logger) : WebhookEventProcessor
{
    private static readonly string[] HeadersToLog =
    [
        "Accept",
        "Content-Type",
        "User-Agent",
        "X-GitHub-Delivery",
        "X-GitHub-Event",
        "X-GitHub-Hook-ID",
        "X-GitHub-Hook-Installation-Target-ID",
        "X-GitHub-Hook-Installation-Target-Type",
        "X-Hub-Signature",
        "X-Hub-Signature-256",
    ];

    public override async ValueTask ProcessWebhookAsync(
        IDictionary<string, StringValues> headers,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(body);

        if (options.CurrentValue.Disable)
        {
            return;
        }

        var rawHeaders = GetHeadersToLog(headers);

        JsonElement? rawPayload = null;

        if (connections.HasConnections)
        {
            rawPayload = await BroadcastLogAsync(rawHeaders, body, cancellationToken);
        }

        var webhookHeaders = WebhookHeaders.Parse(headers);

        metrics.WebhookDelivery(webhookHeaders.Event, webhookHeaders.HookInstallationTargetId);

        var webhookEvent = DeserializeWebhookEvent(webhookHeaders, body);

        using (logger.BeginWebhookScope(webhookHeaders, webhookEvent))
        {
            Log.ReceivedWebhook(logger, webhookHeaders.Delivery);

            GitHubEvent payload = rawPayload is { } parsed
                ? new(webhookHeaders, webhookEvent, rawHeaders, body, parsed)
                : new(webhookHeaders, webhookEvent, rawHeaders, body);

            await handler.HandleAsync(payload, cancellationToken);

            Log.ProcessedWebhook(logger, webhookHeaders.Delivery);
        }
    }

    public override WebhookEvent DeserializeWebhookEvent(WebhookHeaders headers, string body)
    {
        try
        {
            return base.DeserializeWebhookEvent(headers, body);
        }
        catch (JsonException ex)
        {
            using (logger.BeginWebhookScope(headers))
            {
                Log.WebhookDeserializationFailed(logger, headers.Delivery, ex);
            }

            throw;
        }
    }

    private static Dictionary<string, string> GetHeadersToLog(IDictionary<string, StringValues> headers)
    {
        var webhookHeaders = new Dictionary<string, string>(HeadersToLog.Length);

        foreach (string header in HeadersToLog)
        {
            if (headers.TryGetValue(header, out var value))
            {
                webhookHeaders[header] = value!;
            }
        }

        return webhookHeaders;
    }

    private async Task<JsonElement?> BroadcastLogAsync(
        IDictionary<string, string> headers,
        string body,
        CancellationToken cancellationToken)
    {
        try
        {
            // Cannot serialize the parsed webhook objects as-is because DateTimeOffset does
            // not support being serialized and throws an exception, which breaks the SignalR connection.
            // See https://github.com/octokit/webhooks.net/blob/1a6ce29f8312c555227703057ba45723e3c78574/src/Octokit.Webhooks/Converter/DateTimeOffsetConverter.cs#L14.
            var document = JsonElement.Parse(body);

            await hub.Clients.All.SendAsync(WebhookClientMethods.Webhook, headers, document, cancellationToken);

            return document;
        }
        catch (Exception)
        {
            // Swallow exception
            return null;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Received webhook with ID {HookId}.")]
        public static partial void ReceivedWebhook(ILogger logger, string? hookId);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Processed webhook with ID {HookId}.")]
        public static partial void ProcessedWebhook(ILogger logger, string? hookId);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Failed to deserialize webhook with ID {HookId}.")]
        public static partial void WebhookDeserializationFailed(ILogger logger, string? hookId, Exception exception);
    }
}
