﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Threading.Channels;

namespace MartinCostello.Costellobot;

public abstract class ChannelQueue<T>
{
    private const int Capacity = 20;

    private readonly ConcurrentQueue<T> _history;
    private readonly Channel<T> _queue;

    protected ChannelQueue()
    {
        var channelOptions = new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        };

        _queue = Channel.CreateBounded<T>(channelOptions);
        _history = new ConcurrentQueue<T>();
    }

    public virtual async Task<T?> DequeueAsync(CancellationToken cancellationToken)
    {
        T? item = default;

        try
        {
            if (await _queue.Reader.WaitToReadAsync(cancellationToken))
            {
                item = await _queue.Reader.ReadAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }

        return item;
    }

    public virtual bool Enqueue(T item)
    {
        bool written = _queue.Writer.TryWrite(item);

        if (written)
        {
            _history.Enqueue(item);

            while (_history.Count > Capacity)
            {
                _ = _history.TryDequeue(out _);
            }
        }

        return written;
    }

    public IList<T> History() => [.. _history];
}
