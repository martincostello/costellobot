// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed partial class InMemoryGitHubJob(
    GitHubWebhookQueue queue,
    IServiceProvider serviceProvider,
    ILogger<InMemoryGitHubJob> logger) : IGitHubJob
{
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = await queue.DequeueAsync(stoppingToken);

                if (message is null)
                {
                    break;
                }

                await ProcessAsync(message);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == stoppingToken)
            {
                break;
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.SignalCompletion();

        try
        {
            await queue.WaitForQueueToDrainAsync().WaitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.FailedToDrainQueue(logger, ex);
        }
    }

    public async Task ProcessAsync(GitHubEvent message)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();

            var dispatcher = scope.ServiceProvider.GetRequiredService<GitHubWebhookDispatcher>();
            await dispatcher.DispatchAsync(message);
        }
        catch (Exception ex)
        {
            Log.ProcessingFailed(logger, ex, message.Headers.Delivery);
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

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Error,
           Message = "Failed to process webhook with ID {HookId}.")]
        public static partial void ProcessingFailed(ILogger logger, Exception exception, string? hookId);
    }
}
