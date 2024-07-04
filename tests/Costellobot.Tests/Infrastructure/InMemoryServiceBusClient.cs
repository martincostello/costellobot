// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;

namespace MartinCostello.Costellobot.Infrastructure;

internal sealed class InMemoryServiceBusClient : ServiceBusClient
{
    private readonly MessageQueue _queue = new();

    public override ServiceBusProcessor CreateProcessor(string queueName)
        => new InMemoryServiceBusProcessor(_queue);

    public override ServiceBusSender CreateSender(string queueOrTopicName)
        => new InMemoryServiceBusSender(_queue);

#pragma warning disable CA2215
    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
#pragma warning restore CA2215

    private sealed class InMemoryServiceBusProcessor(MessageQueue queue) : ServiceBusProcessor(), IDisposable
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
                    var payload = await queue.DequeueAsync(stoppingToken);

                    if (payload is null)
                    {
                        break;
                    }

                    var data = payload.Body.ToArray();
                    var body = AmqpMessageBody.FromData([data]);
                    var amqp = new AmqpAnnotatedMessage(body);

                    amqp.Properties.ContentType = payload.ContentType;
                    amqp.Properties.MessageId = new AmqpMessageId(payload.MessageId);
                    amqp.Properties.Subject = payload.Subject;

                    var message = ServiceBusReceivedMessage.FromAmqpMessage(amqp, BinaryData.FromString(string.Empty));

                    await using var receiver = new InMemoryServiceBusReceiver();

                    var args = new ProcessMessageEventArgs(
                        message,
                        receiver,
                        nameof(InMemoryServiceBusClient),
                        CancellationToken.None);

                    await OnProcessMessageAsync(args);
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

    private sealed class InMemoryServiceBusSender(MessageQueue queue) : ServiceBusSender
    {
        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            queue.Enqueue(new(message.MessageId, message.ContentType, message.Subject, message.Body));
            return Task.CompletedTask;
        }
    }

    private sealed class MessageQueue : ChannelQueue<Payload>;

    private sealed record Payload(string MessageId, string ContentType, string Subject, BinaryData Body);
}
