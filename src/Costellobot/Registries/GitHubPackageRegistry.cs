// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;
using Octokit.GraphQL;
using IConnection = Octokit.GraphQL.IConnection;

namespace MartinCostello.Costellobot.Registries;

public abstract class GitHubPackageRegistry(GitHubWebhookContext context) : IPackageRegistry
{
    public abstract DependencyEcosystem Ecosystem { get; }

    protected IConnection GraphQLClient => context.GraphQLClient;

    protected IGitHubClient RestClient => context.InstallationClient;

    public virtual async Task<bool> AreOwnersTrustedAsync(
        IReadOnlyList<string> owners,
        CancellationToken cancellationToken)
    {
        if (owners.Count != 1)
        {
            return false;
        }

        return await IsGitHubStarAsync(owners[0], cancellationToken);
    }

    public abstract Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken);

    protected async Task<bool> IsGitHubStarAsync(string login, CancellationToken cancellationToken)
    {
        var query = new Query()
            .User(login)
            .Select((p) => new { p.IsGitHubStar })
            .Compile();

        try
        {
            var result = await GraphQLClient.Run(query, cancellationToken: cancellationToken);
            return result.IsGitHubStar;
        }
        catch (Octokit.GraphQL.Core.GraphQLException)
        {
            return false;
        }
    }
}
