// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;
using Octokit.GraphQL;
using IConnection = Octokit.GraphQL.IConnection;

namespace MartinCostello.Costellobot.Registries;

public abstract class GitHubPackageRegistry : IPackageRegistry
{
    protected GitHubPackageRegistry(
        IGitHubClientForInstallation client,
        IConnection connection)
    {
        RestClient = client;
        GraphQLClient = connection;
    }

    public abstract DependencyEcosystem Ecosystem { get; }

    protected IConnection GraphQLClient { get; }

    protected IGitHubClient RestClient { get; }

    public virtual async Task<bool> AreOwnersTrustedAsync(IReadOnlyList<string> owners)
    {
        if (owners.Count != 1)
        {
            return false;
        }

        return await IsGitHubStarAsync(owners[0]);
    }

    public abstract Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        string owner,
        string repository,
        string id,
        string version);

    protected async Task<bool> IsGitHubStarAsync(string login)
    {
        var query = new Query()
            .User(login)
            .Select((p) => new { p.IsGitHubStar })
            .Compile();

        try
        {
            var result = await GraphQLClient.Run(query);
            return result.IsGitHubStar;
        }
        catch (Octokit.GraphQL.Core.GraphQLException)
        {
            return false;
        }
    }
}
