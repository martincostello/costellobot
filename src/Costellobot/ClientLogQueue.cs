// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Threading.Channels;

namespace MartinCostello.Costellobot;

public sealed partial class ClientLogQueue
{
    private const int QueueCapacity = 1000;

    private readonly Channel<ClientLogMessage> _queue;

    public ClientLogQueue()
    {
        var channelOptions = new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        };

        _queue = Channel.CreateBounded<ClientLogMessage>(channelOptions);
    }

    public async Task<ClientLogMessage?> DequeueAsync(CancellationToken cancellationToken)
    {
        ClientLogMessage? logEntry = null;

        if (await _queue.Reader.WaitToReadAsync(cancellationToken))
        {
            logEntry = await _queue.Reader.ReadAsync(cancellationToken);
        }

        return logEntry;
    }

    public void Enqueue(ClientLogMessage logEntry)
    {
        _ = _queue.Writer.TryWrite(logEntry);
    }
}
