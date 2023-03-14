// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed partial class GitHubWebhookService : IHostedService, IDisposable
{
    private readonly GitHubWebhookQueue _queue;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    private CancellationTokenSource? _cts;
    private Task? _executeTask;

    public GitHubWebhookService(
        GitHubWebhookQueue queue,
        IServiceProvider serviceProvider,
        ILogger<GitHubWebhookService> logger)
    {
        _queue = queue;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public void Dispose()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executeTask = ExecuteAsync(_cts.Token);

        if (_executeTask.IsCompleted)
        {
            return _executeTask;
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.SignalCompletion();

        try
        {
            if (_cts is not null)
            {
                await _cts.CancelAsync();
            }

            await _queue.WaitForQueueToDrainAsync().WaitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.FailedToDrainQueue(_logger, ex);
        }
    }

    public async Task ProcessAsync(GitHubEvent message)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();

            var dispatcher = scope.ServiceProvider.GetRequiredService<GitHubWebhookDispatcher>();
            await dispatcher.DispatchAsync(message);
        }
        catch (Exception ex)
        {
            Log.ProcessingFailed(_logger, ex, message.Headers.Delivery);
        }
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            var message = await _queue.DequeueAsync(stoppingToken);

            if (message is null)
            {
                break;
            }

            await ProcessAsync(message);
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
