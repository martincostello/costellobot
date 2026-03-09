// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using MartinCostello.Costellobot.Models;

namespace MartinCostello.Costellobot.Infrastructure;

internal sealed class InMemoryTrustStore : ITrustStore
{
    private readonly ConcurrentDictionary<(DependencyEcosystem Ecosystem, string Id, string Version), bool> _trustStore = new();

    public int DeniedCount => _trustStore.Values.Count((p) => !p);

    public int TrustedCount => _trustStore.Values.Count((p) => p);

    public void Reset() => _trustStore.Clear();

    public Task DenyAsync(DependencyEcosystem ecosystem, string id, string version, CancellationToken cancellationToken = default)
    {
        _trustStore[(ecosystem, id, version)] = false;
        return Task.CompletedTask;
    }

    public Task UndenyAsync(DependencyEcosystem ecosystem, string id, string version, CancellationToken cancellationToken = default)
    {
        _trustStore.Remove((ecosystem, id, version), out _);
        return Task.CompletedTask;
    }

    public Task DistrustAllAsync(CancellationToken cancellationToken = default)
    {
        _trustStore.Clear();
        return Task.CompletedTask;
    }

    public Task DistrustAsync(DependencyEcosystem ecosystem, string id, string version, CancellationToken cancellationToken = default)
    {
        _trustStore.Remove((ecosystem, id, version), out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeniedDependency>> GetDeniedAsync(DependencyEcosystem ecosystem, CancellationToken cancellationToken = default)
    {
        var denied = _trustStore
            .Where((p) => p.Key.Ecosystem == ecosystem && !p.Value)
            .Select((p) => p.Key)
            .Select((p) => new DeniedDependency(p.Id, p.Version))
            .ToList();

        return Task.FromResult<IReadOnlyList<DeniedDependency>>(denied);
    }

    public Task<IReadOnlyList<TrustedDependency>> GetTrustAsync(DependencyEcosystem ecosystem, CancellationToken cancellationToken = default)
    {
        var trusted = _trustStore
            .Where((p) => p.Key.Ecosystem == ecosystem && p.Value)
            .Select((p) => p.Key)
            .Select((p) => new TrustedDependency(p.Id, p.Version))
            .ToList();

        return Task.FromResult<IReadOnlyList<TrustedDependency>>(trusted);
    }

    public Task<bool> IsDeniedAsync(DependencyEcosystem ecosystem, string id, string version, CancellationToken cancellationToken = default)
    {
        bool exists = _trustStore.TryGetValue((ecosystem, id, version), out var value);
        return Task.FromResult(exists && !value);
    }

    public Task<bool> IsTrustedAsync(DependencyEcosystem ecosystem, string id, string version, CancellationToken cancellationToken = default)
    {
        bool exists = _trustStore.TryGetValue((ecosystem, id, version), out var value);
        return Task.FromResult(exists && value);
    }

    public Task TrustAsync(DependencyEcosystem ecosystem, string id, string version, CancellationToken cancellationToken = default)
    {
        _trustStore[(ecosystem, id, version)] = true;
        return Task.CompletedTask;
    }
}
