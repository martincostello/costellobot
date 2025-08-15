// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace MartinCostello.Costellobot.Registries;

public sealed class GitHubActionsPackageRegistry(
    GitHubWebhookContext context,
    HybridCache cache) : GitHubPackageRegistry(context, cache)
{
    private static readonly string[] CacheTags = ["all", "github-actions"];

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.GitHubActions;

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

            // GitHub Actions tags that are versions are usually prefixed with
            // a 'v' but the version extracted from the commit message will just
            // have the version number. Based on what we find, prefer searching
            // for a tag prefixed with 'v' before looking for the verbatim tag.
            bool hasVersionPrefix = version.StartsWith('v');

            string[] refs = hasVersionPrefix ?
                [version, version[1..]] :
                [$"v{version}", version];

            foreach (var reference in refs)
            {
                if (await CachedExistsAsync($"{owner}/{name}@ref:{reference}", () => RestClient.Git.Reference.Get(owner, name, $"tags/{reference}")))
                {
                    return [owner];
                }
            }

            // If we didn't find the tag(s), maybe it's a sha to a specific commit
            if (await CachedExistsAsync($"{owner}/{name}@commit:{version}", () => RestClient.Git.Commit.Get(owner, name, version)))
            {
                return [owner];
            }
        }

        return [];

        async Task<bool> CachedExistsAsync<T>(string key, Func<Task<T>> resource)
        {
            return await base.CachedExistsAsync<T>(
                key,
                resource,
                CacheTags,
                cancellationToken);
        }
    }
}
