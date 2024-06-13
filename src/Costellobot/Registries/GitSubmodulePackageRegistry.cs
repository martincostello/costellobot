// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;

namespace MartinCostello.Costellobot.Registries;

public sealed class GitSubmodulePackageRegistry(
    IGitHubClientForInstallation client,
    Octokit.GraphQL.IConnection connection) : GitHubPackageRegistry(client, connection)
{
    public override DependencyEcosystem Ecosystem => DependencyEcosystem.Submodules;

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version)
    {
        IReadOnlyList<RepositoryContent> items;

        try
        {
            items = await RestClient.Repository.Content.GetAllContents(repository.Owner, repository.Name, id);
        }
        catch (NotFoundException)
        {
            items = [];
        }

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
