// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Models;

namespace MartinCostello.Costellobot.Infrastructure;

internal sealed class InMemoryTrustStore : ITrustStore
{
    private readonly HashSet<(DependencyEcosystem Ecosystem, string Id, string Version)> _trustStore = [];

    public int Count => _trustStore.Count;

    public Task DistrustAsync(DependencyEcosystem ecosystem, string id, string version, CancellationToken cancellationToken = default)
    {
        _trustStore.Remove((ecosystem, id, version));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TrustedDependency>> GetTrustAsync(DependencyEcosystem ecosystem, CancellationToken cancellationToken = default)
    {
        var trusted = _trustStore
            .Where((p) => p.Ecosystem == ecosystem)
            .Select((p) => new TrustedDependency(p.Id, p.Version))
            .ToList();

        return Task.FromResult<IReadOnlyList<TrustedDependency>>(trusted);
    }

    public Task<bool> IsTrustedAsync(DependencyEcosystem ecosystem, string id, string version, CancellationToken cancellationToken = default)
    {
        bool isTrusted = _trustStore.Contains((ecosystem, id, version));
        return Task.FromResult(isTrusted);
    }

    public Task TrustAsync(DependencyEcosystem ecosystem, string id, string version, CancellationToken cancellationToken = default)
    {
        _trustStore.Add((ecosystem, id, version));
        return Task.CompletedTask;
    }

    public void Clear() => _trustStore.Clear();
}
