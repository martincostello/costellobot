// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Models;

public sealed class DependenciesModel(
    IReadOnlyDictionary<DependencyEcosystem, IReadOnlyList<TrustedDependency>> trusted,
    IReadOnlyDictionary<DependencyEcosystem, IReadOnlyList<DeniedDependency>> denied)
{
    public IReadOnlyDictionary<DependencyEcosystem, IReadOnlyList<TrustedDependency>> Trusted { get; } = trusted;

    public IReadOnlyDictionary<DependencyEcosystem, IReadOnlyList<DeniedDependency>> Denied { get; } = denied;
}
