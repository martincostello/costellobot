// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace Octokit;

public sealed class InstallationCredentialStore(
    IGitHubClientForApp client,
    HybridCache cache) : CredentialStore, GraphQL.ICredentialStore
{
    public required long InstallationId { get; init; }

    public override async Task<Credentials> GetCredentials()
    {
        // See https://docs.github.com/apps/creating-github-apps/authenticating-with-a-github-app/about-authentication-with-a-github-app#authentication-as-an-app-installation
        var token = await cache.GetOrCreateAsync(
            $"github:installation-credentials:{InstallationId}",
            (InstallationId, client.GitHubApps),
            static async (state, _) =>
            {
                var accessToken = await state.GitHubApps.CreateInstallationToken(state.InstallationId);
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
