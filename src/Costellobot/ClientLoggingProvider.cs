// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;

namespace MartinCostello.Costellobot;

public sealed class ClientLoggingProvider : ILoggerProvider
{
    internal const string CategoryPrefix = "MartinCostello.Costellobot";

    private readonly TimeProvider _timeProvider;
    private readonly ClientLogQueue _queue;

    public ClientLoggingProvider(ClientLogQueue queue, TimeProvider timeProvider)
    {
        _queue = queue;
        _timeProvider = timeProvider;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return
            categoryName.StartsWith(CategoryPrefix, StringComparison.Ordinal) ?
            new ClientLogger(categoryName, _queue, _timeProvider) :
            NullLoggerFactory.Instance.CreateLogger(categoryName);
    }

    public void Dispose()
    {
        // No-op
    }
}
