// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubPublisherProcessor(
    ServiceBusClient client,
    IHubContext<GitHubWebhookHub, IWebhookClient> hub,
    IOptions<WebhookOptions> options,
    ILogger<GitHubPublisherProcessor> logger) : WebhookEventProcessor
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

    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);

    public override async Task ProcessWebhookAsync(IDictionary<string, StringValues> headers, string body)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(body);

        await BroadcastLogAsync(headers, body);

        var webhookHeaders = WebhookHeaders.Parse(headers);

        Log.ReceivedWebhook(logger, webhookHeaders.Delivery);

        using var cts = new CancellationTokenSource(PublishTimeout);

        try
        {
            var message = GitHubMessageSerializer.Serialize(webhookHeaders.Delivery, headers, body);

            var sender = client.CreateSender(options.Value.QueueName);
            await sender.SendMessageAsync(message, cts.Token);
        }
        catch (Exception ex)
        {
            Log.PublishFailed(logger, ex, webhookHeaders.Delivery);
        }
    }

    private async Task<(IDictionary<string, string> Headers, JsonElement Payload)> BroadcastLogAsync(
        IDictionary<string, StringValues> headers,
        string body)
    {
        try
        {
            // HACK Cannot serialize the parsed webhook objects as-is because DateTimeOffset does
            // not support being serialized and throws an exception, which breaks the SignalR connection.
            // See https://github.com/octokit/webhooks.net/blob/1a6ce29f8312c555227703057ba45723e3c78574/src/Octokit.Webhooks/Converter/DateTimeOffsetConverter.cs#L14.
            using var document = JsonDocument.Parse(body);

            var webhookHeaders = new Dictionary<string, string>(HeadersToLog.Length);

            foreach (string header in HeadersToLog)
            {
                if (headers.TryGetValue(header, out var value))
                {
                    webhookHeaders[header] = value!;
                }
            }

            await hub.Clients.All.WebhookAsync(webhookHeaders, document.RootElement);

            return (webhookHeaders, document.RootElement.Clone());
        }
        catch (Exception)
        {
            // Swallow exception
            return default;
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
           Level = LogLevel.Error,
           Message = "Failed to publish message for webhook with ID {HookId}.")]
        public static partial void PublishFailed(ILogger logger, Exception exception, string? hookId);
    }
}
