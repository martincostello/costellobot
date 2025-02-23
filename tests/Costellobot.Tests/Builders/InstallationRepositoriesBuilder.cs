// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class InstallationRepositoriesBuilder(IEnumerable<RepositoryBuilder> repositories) : ResponseBuilder
{
    public IList<RepositoryBuilder> Repositories { get; } = [.. repositories];

    public override object Build()
    {
        return new
        {
            total_count = Repositories.Count,
            repositories = Repositories.Select((p) => p.Build()).ToArray(),
        };
    }
}
