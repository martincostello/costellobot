// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubEventProcessor : WebhookEventProcessor
{
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

    public override async Task ProcessWebhookAsync(WebhookHeaders headers, WebhookEvent webhookEvent)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(webhookEvent);

        Log.ReceivedWebhook(_logger, headers.HookId);
        _queue.Enqueue(new(headers, webhookEvent));

        await _hub.Clients.All.WebhookAsync(headers, webhookEvent);
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
