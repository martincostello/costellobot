// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubEventProcessor : WebhookEventProcessor
{
    private static readonly string[] HeadersToLog =
    {
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
    };

    private readonly IHubContext<GitHubWebhookHub, IWebhookClient> _hub;
    private readonly ILogger<GitHubEventProcessor> _logger;
    private readonly GitHubWebhookQueue _queue;

    public GitHubEventProcessor(
        GitHubWebhookQueue queue,
        IHubContext<GitHubWebhookHub, IWebhookClient> hub,
        ILogger<GitHubEventProcessor> logger)
    {
        _queue = queue;
        _hub = hub;
        _logger = logger;
    }

    public override async Task ProcessWebhookAsync(IDictionary<string, StringValues> headers, string body)
    {
        await BroadcastLogAsync(headers, body);
        await base.ProcessWebhookAsync(headers, body);
    }

    public override Task ProcessWebhookAsync(WebhookHeaders headers, WebhookEvent webhookEvent)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(webhookEvent);

        Log.ReceivedWebhook(_logger, headers.HookId);
        _queue.Enqueue(new(headers, webhookEvent));

        return Task.CompletedTask;
    }

    private async Task BroadcastLogAsync(IDictionary<string, StringValues> headers, string body)
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
                webhookHeaders[header] = headers[header];
            }

            await _hub.Clients.All.WebhookAsync(webhookHeaders, document.RootElement);
        }
        catch (Exception)
        {
            // Swallow exception
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Received webhook with ID {HookId}.")]
        public static partial void ReceivedWebhook(ILogger logger, string? hookId);
    }
}
