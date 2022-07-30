// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class TrustedEntitiesOptions
{
    public IList<string> Dependencies { get; set; } = new List<string>();

    public IDictionary<DependencyEcosystem, IList<string>> Publishers { get; set; } = new Dictionary<DependencyEcosystem, IList<string>>();

    public IList<string> Users { get; set; } = new List<string>();
}
