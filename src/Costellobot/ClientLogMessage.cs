// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class ClientLogMessage
{
    public string Category { get; init; } = string.Empty;

    public string Level { get; init; } = string.Empty;

    public int EventId { get; init; }

    public string? EventName { get; init; }

    public string Message { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; }
}
