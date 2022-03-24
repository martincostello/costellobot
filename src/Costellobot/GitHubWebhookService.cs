// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed partial class GitHubWebhookService : IHostedService
{
    private readonly GitHubWebhookQueue _queue;
    private readonly ILogger _logger;

    public GitHubWebhookService(
        GitHubWebhookQueue queue,
        ILogger<GitHubWebhookService> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // TODO Start the processing loop
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.SignalCompletion();

        try
        {
            await _queue.WaitForQueueToDrainAsync().WaitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.FailedToDrainQueue(_logger, ex);
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Warning,
           Message = "Failed to drain webhook queue.")]
        public static partial void FailedToDrainQueue(ILogger logger, Exception exception);
    }
}
