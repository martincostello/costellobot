// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Models;

namespace MartinCostello.Costellobot;

public interface IDenyStore
{
    Task DenyAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default);

    Task AllowAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default);

    Task AllowAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeniedDependency>> GetDeniedAsync(
        DependencyEcosystem ecosystem,
        CancellationToken cancellationToken = default);

    Task<bool> IsDeniedAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default);
}
