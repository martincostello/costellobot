// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;

namespace MartinCostello.Costellobot;

public sealed class ClientLoggingProvider : ILoggerProvider
{
    private readonly IClock _clock;
    private readonly ClientLogQueue _queue;

    public ClientLoggingProvider(ClientLogQueue queue, IClock clock)
    {
        _queue = queue;
        _clock = clock;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return
            categoryName.StartsWith("MartinCostello.Costellobot", StringComparison.Ordinal) ?
            new ClientLogger(categoryName, _queue, _clock) :
            NullLoggerFactory.Instance.CreateLogger(categoryName);
    }

    public void Dispose()
    {
        // No-op
    }
}
