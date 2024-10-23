// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;

namespace MartinCostello.Costellobot;

public sealed class ClientLoggingProvider(ClientLogQueue queue, TimeProvider timeProvider) : ILoggerProvider
{
    internal const string CategoryPrefix = "MartinCostello.Costellobot";

    private readonly ConcurrentDictionary<string, ClientLogger> _loggers = [];

    public ILogger CreateLogger(string categoryName)
    {
        if (!categoryName.StartsWith(CategoryPrefix, StringComparison.Ordinal))
        {
            return NullLogger.Instance;
        }

        return _loggers.GetOrAdd(categoryName, (name) => new(name, queue, timeProvider));
    }

    public void Dispose()
    {
        // No-op
    }
}
