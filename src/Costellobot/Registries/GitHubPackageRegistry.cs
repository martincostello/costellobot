// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Hybrid;
using Octokit;
using Octokit.GraphQL;
using IConnection = Octokit.GraphQL.IConnection;

namespace MartinCostello.Costellobot.Registries;

public abstract class GitHubPackageRegistry(GitHubWebhookContext context, HybridCache cache) : IPackageRegistry
{
    protected static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(1) };

    public abstract DependencyEcosystem Ecosystem { get; }

    protected HybridCache Cache => cache;

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

    protected (string Owner, string Name) ParseRepository(string repository)
    {
        string[] parts = repository.Split('/');

        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }

        return default;
    }

    protected async Task<bool> CachedExistsAsync<T>(string key, Func<Task<T>> resource, IEnumerable<string> tags, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync<bool>(
            key,
            async (_) => await ExistsAsync<T>(resource),
            CacheEntryOptions,
            tags,
            cancellationToken);
    }

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

    private static async Task<bool> ExistsAsync<T>(Func<Task<T>> resource)
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
}
