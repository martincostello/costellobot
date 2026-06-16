// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Models;

/// <summary>
/// Represents a dependency ordered for display along with whether it has been
/// superseded by a more recent version of the same dependency.
/// </summary>
/// <typeparam name="T">The type of the dependency.</typeparam>
/// <param name="Dependency">The dependency.</param>
/// <param name="IsOutdated">Whether the dependency is outdated.</param>
public sealed record OrderedDependency<T>(T Dependency, bool IsOutdated)
    where T : IDependency;
