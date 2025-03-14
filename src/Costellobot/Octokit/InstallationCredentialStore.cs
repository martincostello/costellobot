// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Octokit;

public sealed class InstallationCredentialStore(
    IGitHubClientForApp client,
    HybridCache cache,
    IOptionsMonitor<GitHubOptions> options) : CredentialStore, GraphQL.ICredentialStore
{
    public override async Task<Credentials> GetCredentials()
    {
        // See https://docs.github.com/apps/creating-github-apps/authenticating-with-a-github-app/about-authentication-with-a-github-app#authentication-as-an-app-installation
        long installationId = options.CurrentValue.InstallationId;

        var token = await cache.GetOrCreateAsync(
            $"github:installation-credentials:{installationId}",
            (installationId, client.GitHubApps),
            static async (state, _) =>
            {
                var accessToken = await state.GitHubApps.CreateInstallationToken(state.installationId);
                return accessToken.Token;
            },
            CacheEntryOptions,
            CacheTags);

        return new Credentials(token, AuthenticationType.Oauth);
    }

    async Task<string> GraphQL.ICredentialStore.GetCredentials(CancellationToken cancellationToken)
    {
        var credentials = await GetCredentials();
        return credentials.GetToken();
    }
}
