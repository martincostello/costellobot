// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace MartinCostello.Costellobot.Registries;

public sealed class GitHubReleasePackageRegistry(
    GitHubWebhookContext context,
    HybridCache cache) : GitHubPackageRegistry(context, cache)
{
    private static readonly string[] CacheTags = ["all", "github-release"];

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.GitHubRelease;

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        var slug = ParseRepository(id);

        if (slug != default)
        {
            string owner = slug.Owner;
            string name = slug.Name;

            var exists = await CachedExistsAsync(
                $"{owner}/{name}@release:{version}",
                () => RestClient.Repository.Release.Get(owner, name, version),
                CacheTags,
                cancellationToken);

            if (exists)
            {
                return [owner];
            }
        }

        return [];
    }
}
