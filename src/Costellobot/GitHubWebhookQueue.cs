﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed partial class GitHubWebhookQueue(ILogger<GitHubWebhookQueue> logger) : ChannelQueue<GitHubEvent>()
{
    public override async Task<GitHubEvent?> DequeueAsync(CancellationToken cancellationToken)
    {
        Log.WaitingForWebhook(logger);

        GitHubEvent? message = await base.DequeueAsync(cancellationToken);

        if (message is not null)
        {
            Log.DequeuedWebhook(logger, message.Headers.Delivery);
        }

        return message;
    }

    public override bool Enqueue(GitHubEvent item)
    {
        bool success = base.Enqueue(item);

        if (success)
        {
            Log.QueuedWebhook(logger, item.Headers.Delivery);
        }
        else
        {
            Log.WebhookQueueFailed(logger, item.Headers.Delivery);
        }

        return success;
    }

    public void SignalCompletion()
    {
        if (Queue.Writer.TryComplete())
        {
            Log.QueueCompleted(logger);
        }
    }

    public async Task WaitForQueueToDrainAsync()
    {
        Log.WaitingForQueueToDrain(logger);

        await Queue.Reader.Completion;

        Log.QueueDrained(logger);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "Waiting for webhook to process from queue.")]
        public static partial void WaitingForWebhook(ILogger logger);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Debug,
           Message = "Queued webhook with ID {HookId} for processing.")]
        public static partial void QueuedWebhook(ILogger logger, string? hookId);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Warning,
           Message = "Failed to queue webhook with ID {HookId} for processing.")]
        public static partial void WebhookQueueFailed(ILogger logger, string? hookId);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Debug,
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
