// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Hybrid;
using Octokit;

namespace MartinCostello.Costellobot.Registries;

public sealed class GitSubmodulePackageRegistry(
    GitHubWebhookContext context,
    HybridCache cache) : GitHubPackageRegistry(context, cache)
{
    private static readonly string[] CacheTags = ["all", "github-submodule"];

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.GitSubmodule;

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<RepositoryContent> items = await Cache.GetOrCreateAsync(
            $"git-submodule:{repository.Owner}/{repository.Name}:{id}",
            (RestClient, repository, id),
            static async (context, _) =>
            {
                try
                {
                    return await context.RestClient.Repository.Content.GetAllContents(
                        context.repository.Owner,
                        context.repository.Name,
                        context.id);
                }
                catch (NotFoundException)
                {
                    return [];
                }
            },
            CacheEntryOptions,
            CacheTags,
            cancellationToken);

        if (items.Count is 1 &&
            items[0] is { SubmoduleGitUrl: not null } item)
        {
            string url = item.SubmoduleGitUrl;
            string urlWithoutRepoName = string.Join('/', url.Split('/').SkipLast(1));

            return [urlWithoutRepoName];
        }

        return [];
    }
}
