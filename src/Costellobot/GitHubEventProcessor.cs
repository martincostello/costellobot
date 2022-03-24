// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Terrajobst.GitHubEvents;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubEventProcessor : IGitHubEventProcessor
{
    private readonly ILogger<GitHubEventProcessor> _logger;
    private readonly GitHubWebhookQueue _queue;

    public GitHubEventProcessor(
        GitHubWebhookQueue queue,
        ILogger<GitHubEventProcessor> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public void Process(GitHubEvent message)
    {
        Log.ReceivedWebhook(_logger, message.HookId);
        _queue.Enqueue(message);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Received webhook with ID {HookId}.")]
        public static partial void ReceivedWebhook(ILogger logger, string hookId);
    }
}
