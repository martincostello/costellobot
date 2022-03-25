// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Threading.Channels;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubWebhookQueue
{
    private const int QueueCapacity = 1000;

    private readonly Channel<GitHubEvent> _queue;
    private readonly ILogger _logger;

    public GitHubWebhookQueue(ILogger<GitHubWebhookQueue> logger)
    {
        var channelOptions = new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        };

        _queue = Channel.CreateBounded<GitHubEvent>(channelOptions);
        _logger = logger;
    }

    public async Task<GitHubEvent?> DequeueAsync(CancellationToken cancellationToken)
    {
        Log.WaitingForWebhook(_logger);

        GitHubEvent? message = null;

        if (await _queue.Reader.WaitToReadAsync(cancellationToken))
        {
            message = await _queue.Reader.ReadAsync(cancellationToken);
            Log.DequeuedWebhook(_logger, message.Headers.HookId);
        }

        return message;
    }

    public void Enqueue(GitHubEvent message)
    {
        if (_queue.Writer.TryWrite(message))
        {
            Log.QueuedWebhook(_logger, message.Headers.HookId);
        }
        else
        {
            Log.WebhookQueueFailed(_logger, message.Headers.HookId);
        }
    }

    public void SignalCompletion()
    {
        if (_queue.Writer.TryComplete())
        {
            Log.QueueCompleted(_logger);
        }
    }

    public async Task WaitForQueueToDrainAsync()
    {
        Log.WaitingForQueueToDrain(_logger);

        await _queue.Reader.Completion;

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
