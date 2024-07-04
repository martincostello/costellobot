// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Threading.Channels;
using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace MartinCostello.Costellobot.Infrastructure;

internal sealed class InMemoryServiceBusClient : ServiceBusClient
{
    private readonly Channel<Payload> _channel;
    private readonly string _connectionString;
    private readonly string _queueName;

    public InMemoryServiceBusClient(IConfiguration configuration)
    {
        _connectionString = configuration["ConnectionStrings:AzureServiceBus"] ?? string.Empty;
        _queueName = configuration["Webhook:QueueName"] ?? string.Empty;

        _channel = Channel.CreateUnbounded<Payload>(new()
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public override ServiceBusProcessor CreateProcessor(string queueName)
        => new InMemoryServiceBusProcessor(_channel.Reader, _connectionString, _queueName);

    public override ServiceBusSender CreateSender(string queueOrTopicName)
        => new InMemoryServiceBusSender(_channel.Writer);

#pragma warning disable CA2215
    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
#pragma warning restore CA2215

    private sealed class InMemoryServiceBusProcessor(
        ChannelReader<Payload> reader,
        string connectionString,
        string queueName) : ServiceBusProcessor(), IDisposable
    {
        private CancellationTokenSource? _cts;
        private Task? _executeTask;

        public void Dispose()
        {
            if (_cts is not null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        public override Task StartProcessingAsync(CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executeTask = ExecuteAsync(_cts.Token);

            if (_executeTask.IsCompleted)
            {
                return _executeTask;
            }

            return Task.CompletedTask;
        }

        public override async Task StopProcessingAsync(CancellationToken cancellationToken = default)
        {
            if (_cts is not null)
            {
                await _cts.CancelAsync();
                _cts = null;
            }
        }

        private async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var payload = await reader.ReadAsync(stoppingToken);

                    if (payload is null)
                    {
                        break;
                    }

                    var body = AmqpMessageBody.FromData([payload.Body]);
                    var amqp = new AmqpAnnotatedMessage(body);

                    amqp.Properties.ContentType = payload.ContentType;
                    amqp.Properties.MessageId = new AmqpMessageId(payload.MessageId);
                    amqp.Properties.Subject = payload.Subject;

                    var message = ServiceBusReceivedMessage.FromAmqpMessage(amqp, BinaryData.FromBytes([]));

                    var identifier = nameof(InMemoryServiceBusClient);

                    try
                    {
                        await using var receiver = new InMemoryServiceBusReceiver();

                        var args = new ProcessMessageEventArgs(
                            message,
                            receiver,
                            identifier,
                            CancellationToken.None);

                        await OnProcessMessageAsync(args);
                    }
                    catch (Exception ex)
                    {
                        var args = new ProcessErrorEventArgs(
                            ex,
                            ServiceBusErrorSource.ProcessMessageCallback,
                            connectionString,
                            queueName,
                            identifier,
                            stoppingToken);

                        await OnProcessErrorAsync(args);
                    }
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == stoppingToken)
                {
                    break;
                }
            }
        }
    }

    private sealed class InMemoryServiceBusReceiver : ServiceBusReceiver
    {
        public override Task CloseAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InMemoryServiceBusSender(ChannelWriter<Payload> writer) : ServiceBusSender
    {
        public override async Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
            => await writer.WriteAsync(new(message.MessageId, message.ContentType, message.Subject, message.Body.ToArray()), cancellationToken);
    }

    private sealed record Payload(string MessageId, string ContentType, string Subject, byte[] Body);
}
