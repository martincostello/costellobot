// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using MartinCostello.Costellobot.Models;

namespace MartinCostello.Costellobot.Infrastructure;

internal sealed class InMemoryDenyStore : IDenyStore
{
    private readonly ConcurrentDictionary<(DependencyEcosystem Ecosystem, string Id, string Version), bool> _denyStore = new();

    public int Count => _denyStore.Count;

    public Task AllowAllAsync(CancellationToken cancellationToken = default)
    {
        _denyStore.Clear();
        return Task.CompletedTask;
    }

    public Task AllowAsync(DependencyEcosystem ecosystem, string id, string version, CancellationToken cancellationToken = default)
    {
        _denyStore.Remove((ecosystem, id, version), out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeniedDependency>> GetDeniedAsync(DependencyEcosystem ecosystem, CancellationToken cancellationToken = default)
    {
        var denied = _denyStore.Keys
            .Where((p) => p.Ecosystem == ecosystem)
            .Select((p) => new DeniedDependency(p.Id, p.Version))
            .ToList();

        return Task.FromResult<IReadOnlyList<DeniedDependency>>(denied);
    }

    public Task<bool> IsDeniedAsync(DependencyEcosystem ecosystem, string id, string version, CancellationToken cancellationToken = default)
    {
        bool isDenied = _denyStore.ContainsKey((ecosystem, id, version));
        return Task.FromResult(isDenied);
    }

    public Task DenyAsync(DependencyEcosystem ecosystem, string id, string version, CancellationToken cancellationToken = default)
    {
        _denyStore[(ecosystem, id, version)] = true;
        return Task.CompletedTask;
    }
}
