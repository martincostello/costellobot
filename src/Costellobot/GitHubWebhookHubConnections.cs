// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

/// <summary>
/// A class that tracks the number of clients connected to <see cref="GitHubWebhookHub"/>.
/// </summary>
/// <remarks>
/// Broadcasts to the hub are skipped when no clients are connected to avoid
/// parsing and serializing payloads that would never be sent to anyone.
/// </remarks>
public sealed class GitHubWebhookHubConnections
{
    private int _count;

    /// <summary>
    /// Gets a value indicating whether any clients are currently connected.
    /// </summary>
    public bool HasConnections => Volatile.Read(ref _count) > 0;

    internal void Connected() => Interlocked.Increment(ref _count);

    internal void Disconnected() => Interlocked.Decrement(ref _count);
}
