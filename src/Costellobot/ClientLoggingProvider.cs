// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;

namespace MartinCostello.Costellobot;

public sealed class ClientLoggingProvider(ClientLogQueue queue, TimeProvider timeProvider) : ILoggerProvider
{
    internal const string CategoryPrefix = "MartinCostello.Costellobot";

    public ILogger CreateLogger(string categoryName)
    {
        return
            categoryName.StartsWith(CategoryPrefix, StringComparison.Ordinal) ?
            new ClientLogger(categoryName, queue, timeProvider) :
            NullLoggerFactory.Instance.CreateLogger(categoryName);
    }

    public void Dispose()
    {
        // No-op
    }
}
