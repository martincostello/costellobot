// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubMessageService(
    ServiceBusClient client,
    GitHubMessageProcessor processor,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<GitHubMessageService> logger) : IHostedService, IAsyncDisposable
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
            if (_processor is { } processor)
            {
                await processor.StopProcessingAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Log.FailedToStopProcessor(logger, ex);
        }
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
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
            return;
        }
    }

    private async Task ProcessAsync(ProcessMessageEventArgs args)
    {
        if (!string.Equals(args.Message.ContentType, GitHubMessage.ContentType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Message with ID {args.Message.MessageId} has an invalid content type: {args.Message.ContentType}.");
        }

        if (!string.Equals(args.Message.Subject, GitHubMessage.Subject, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Message with ID {args.Message.MessageId} has an invalid subject: {args.Message.Subject}.");
        }

        var message = JsonSerializer.Deserialize(args.Message.Body, MessagingJsonSerializerContext.Default.GitHubMessage)!;

        var headers = new Dictionary<string, StringValues>(message.Headers.Count, StringComparer.OrdinalIgnoreCase);

        foreach ((var key, var values) in message.Headers)
        {
            headers[key] = new(values);
        }

        await processor.ProcessWebhookAsync(headers, message.Body);

        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
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
           Message = "Failed to stop message processor.")]
        public static partial void FailedToStopProcessor(ILogger logger, Exception exception);

        [LoggerMessage(
           EventId = 2,
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
