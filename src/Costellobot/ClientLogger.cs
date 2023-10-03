// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class ClientLogger(string categoryName, ClientLogQueue queue, TimeProvider timeProvider) : ILogger
{
    public string CategoryName { get; } = categoryName[ClientLoggingProvider.CategoryPrefix.Length..].TrimStart('.');

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => NullDisposable.Instance;

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

        string levelString = logLevel switch
        {
            LogLevel.Information => "Info",
            LogLevel.Warning => "Warn",
            _ => logLevel.ToString(),
        };

        var payload = new ClientLogMessage()
        {
            Category = CategoryName,
            Level = levelString,
            EventId = eventId.Id,
            EventName = eventId.Name,
            Exception = exception?.ToString().ReplaceLineEndings("\n") ?? string.Empty,
            Message = formatter(state, exception),
            Timestamp = timeProvider.GetUtcNow(),
        };

        queue.Enqueue(payload);
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
