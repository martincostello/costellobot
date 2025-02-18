// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Models;

public sealed class DependenciesModel(IReadOnlyDictionary<DependencyEcosystem, IReadOnlyList<KeyValuePair<string, string>>> dependencies)
{
    public IReadOnlyDictionary<DependencyEcosystem, IReadOnlyList<KeyValuePair<string, string>>> Dependencies => dependencies;
}
