// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed partial class GitHubWebhookQueue : ChannelQueue<GitHubEvent>
{
    private readonly ILogger _logger;

    public GitHubWebhookQueue(ILogger<GitHubWebhookQueue> logger)
        : base()
    {
        _logger = logger;
    }

    public override async Task<GitHubEvent?> DequeueAsync(CancellationToken cancellationToken)
    {
        Log.WaitingForWebhook(_logger);

        GitHubEvent? message = await base.DequeueAsync(cancellationToken);

        if (message is not null)
        {
            Log.DequeuedWebhook(_logger, message.Headers.HookId);
        }

        return message;
    }

    public override bool Enqueue(GitHubEvent item)
    {
        bool success = base.Enqueue(item);

        if (success)
        {
            Log.QueuedWebhook(_logger, item.Headers.HookId);
        }
        else
        {
            Log.WebhookQueueFailed(_logger, item.Headers.HookId);
        }

        return success;
    }

    public void SignalCompletion()
    {
        if (Queue.Writer.TryComplete())
        {
            Log.QueueCompleted(_logger);
        }
    }

    public async Task WaitForQueueToDrainAsync()
    {
        Log.WaitingForQueueToDrain(_logger);

        await Queue.Reader.Completion;

        Log.QueueDrained(_logger);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Waiting for webhook to process from queue.")]
        public static partial void WaitingForWebhook(ILogger logger);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Information,
           Message = "Queued webhook with ID {HookId} for processing.")]
        public static partial void QueuedWebhook(ILogger logger, string? hookId);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Warning,
           Message = "Failed to queue webhook with ID {HookId} for processing.")]
        public static partial void WebhookQueueFailed(ILogger logger, string? hookId);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Information,
           Message = "Dequeued webhook with ID {HookId} for processing.")]
        public static partial void DequeuedWebhook(ILogger logger, string? hookId);

        [LoggerMessage(
           EventId = 5,
           Level = LogLevel.Information,
           Message = "Waiting for webhook queue to drain.")]
        public static partial void WaitingForQueueToDrain(ILogger logger);

        [LoggerMessage(
           EventId = 6,
           Level = LogLevel.Information,
           Message = "Webhook queue has been drained.")]
        public static partial void QueueDrained(ILogger logger);

        [LoggerMessage(
           EventId = 7,
           Level = LogLevel.Information,
           Message = "Completion signalled for GitHub webhook queue.")]
        public static partial void QueueCompleted(ILogger logger);
    }
}
