// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubWebhookService(
    ServiceBusClient client,
    GitHubMessageProcessor processor,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<GitHubWebhookService> logger) : IHostedService, IAsyncDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _executeTask;
    private ServiceBusProcessor? _processor;

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }

        if (_processor is not null)
        {
            await _processor.DisposeAsync();
            _processor = null;
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
        try
        {
            if (_cts is not null)
            {
                await _cts.CancelAsync();
            }

            if (_processor is { } processor)
            {
                await processor.StopProcessingAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Log.FailedToStopJob(logger, ex);
        }
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _processor = client.CreateProcessor(options.CurrentValue.QueueName);

                _processor.ProcessErrorAsync += ProcessErrorAsync;
                _processor.ProcessMessageAsync += ProcessAsync;

                await _processor.StartProcessingAsync(stoppingToken);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == stoppingToken)
            {
                break;
            }
        }
    }

    public async Task ProcessAsync(ProcessMessageEventArgs args)
    {
        Log.ReceivedMessage(logger, args.Message.MessageId);

        if (!string.Equals(args.Message.ContentType, GitHubMessage.ContentType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Message with ID {args.Message.MessageId} has an invalid content type: {args.Message.ContentType}.");
        }

        if (!string.Equals(args.Message.Subject, GitHubMessage.Subject, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Message with ID {args.Message.MessageId} has an invalid subject: {args.Message.Subject}.");
        }

        (var headers, var body) = GitHubMessageSerializer.Deserialize(args.Message);

        Log.ProcessingMessage(logger, args.Message.MessageId);

        await processor.ProcessWebhookAsync(headers, body);

        await args.CompleteMessageAsync(args.Message, args.CancellationToken);

        Log.CompletedMessage(logger, args.Message.MessageId);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        Log.ProcessingFailed(
            logger,
            args.Exception,
            args.Identifier,
            args.ErrorSource.ToString(),
            args.EntityPath,
            args.FullyQualifiedNamespace);

        return Task.CompletedTask;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Warning,
           Message = "Failed to stop messaging job.")]
        public static partial void FailedToStopJob(ILogger logger, Exception exception);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Debug,
           Message = "Received message with identifier {Identifier}.")]
        public static partial void ReceivedMessage(ILogger logger, string identifier);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Debug,
           Message = "Processing message with identifier {Identifier}.")]
        public static partial void ProcessingMessage(ILogger logger, string identifier);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Debug,
           Message = "Completed message with identifier {Identifier}.")]
        public static partial void CompletedMessage(ILogger logger, string identifier);

        [LoggerMessage(
           EventId = 5,
           Level = LogLevel.Error,
           Message = "Failed to process message with identifier {Identifier} from source {ErrorSource} for entity {EntityPath} of namespace {FullyQualifiedNamespace}.")]
        public static partial void ProcessingFailed(
            ILogger logger,
            Exception exception,
            string identifier,
            string errorSource,
            string entityPath,
            string fullyQualifiedNamespace);
    }
}
