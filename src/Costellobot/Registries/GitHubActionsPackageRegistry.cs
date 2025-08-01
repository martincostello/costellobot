// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Hybrid;
using Octokit;

namespace MartinCostello.Costellobot.Registries;

public sealed class GitHubActionsPackageRegistry(
    GitHubWebhookContext context,
    HybridCache cache) : GitHubPackageRegistry(context)
{
    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(1) };
    private static readonly string[] CacheTags = ["all", "github-actions"];

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.GitHubActions;

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        string[] parts = id.Split('/');

        if (parts.Length == 2)
        {
            string owner = parts[0];
            string name = parts[1];

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

        static async Task<bool> ExistsAsync<T>(Func<Task<T>> resource)
        {
            try
            {
                _ = await resource();
                return true;
            }
            catch (NotFoundException)
            {
                return false;
            }
        }

        async Task<bool> CachedExistsAsync<T>(string key, Func<Task<T>> resource)
        {
            return await cache.GetOrCreateAsync<bool>(
                key,
                async (_) => await ExistsAsync<T>(resource),
                CacheEntryOptions,
                CacheTags,
                cancellationToken);
        }
    }
}
