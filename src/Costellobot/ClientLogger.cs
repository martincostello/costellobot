// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using NodaTime;

namespace MartinCostello.Costellobot;

public sealed class ClientLogger : ILogger
{
    private readonly IClock _clock;
    private readonly ClientLogQueue _queue;

    public ClientLogger(string categoryName, ClientLogQueue queue, IClock clock)
    {
        CategoryName = categoryName;
        _queue = queue;
        _clock = clock;
    }

    public string CategoryName { get; }

    public IDisposable BeginScope<TState>(TState state) => NullDisposable.Instance;

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Debug => true,
            LogLevel.Error => true,
            LogLevel.Information => true,
            LogLevel.Warning => true,
            _ => false,
        };
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var payload = new ClientLogMessage()
        {
            Category = CategoryName,
            Level = logLevel.ToString(),
            EventId = eventId.Id,
            EventName = eventId.Name,
            Message = formatter(state, exception),
            Timestamp = _clock.GetCurrentInstant().ToDateTimeOffset(),
        };

        _queue.Enqueue(payload);
    }

    private sealed class NullDisposable : IDisposable
    {
        internal static readonly NullDisposable Instance = new();

        public void Dispose()
        {
            // No-op
        }
    }
}
