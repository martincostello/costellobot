﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Models;

namespace MartinCostello.Costellobot;

public interface ITrustStore
{
    Task DistrustAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default);

    Task DistrustAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrustedDependency>> GetTrustAsync(
       DependencyEcosystem ecosystem,
       CancellationToken cancellationToken = default);

    Task<bool> IsTrustedAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default);

    Task TrustAsync(
        DependencyEcosystem ecosystem,
        string id,
        string version,
        CancellationToken cancellationToken = default);
}
