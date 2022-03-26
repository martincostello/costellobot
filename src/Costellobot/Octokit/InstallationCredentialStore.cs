// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Octokit;

public sealed class InstallationCredentialStore : ICredentialStore, GraphQL.ICredentialStore
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan TokenSkew = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;
    private readonly IGitHubClient _client;
    private readonly IOptionsMonitor<GitHubOptions> _options;

    public InstallationCredentialStore(
        IGitHubClientForApp client,
        IMemoryCache cache,
        IOptionsMonitor<GitHubOptions> options)
    {
        _cache = cache;
        _client = client;
        _options = options;
    }

    public async Task<Credentials> GetCredentials()
    {
        // See https://docs.github.com/en/developers/apps/building-github-apps/authenticating-with-github-apps#authenticating-as-an-installation
        long installationId = _options.CurrentValue.InstallationId;

        return await _cache.GetOrCreateAsync($"github:installation-credentials:{installationId}", async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = TokenLifetime - TokenSkew;

            var accessToken = await _client.GitHubApps.CreateInstallationToken(installationId);
            return new Credentials(accessToken.Token, AuthenticationType.Oauth);
        });
    }

    async Task<string> GraphQL.ICredentialStore.GetCredentials(CancellationToken cancellationToken)
    {
        var credentials = await GetCredentials();
        return credentials.GetToken();
    }
}
