// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Octokit;

public sealed class InstallationCredentialStore(
    IGitHubClientForApp client,
    IMemoryCache cache,
    IOptionsMonitor<GitHubOptions> options) : ICredentialStore, GraphQL.ICredentialStore
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan TokenSkew = TimeSpan.FromMinutes(5);

    public async Task<Credentials> GetCredentials()
    {
        // See https://docs.github.com/en/developers/apps/building-github-apps/authenticating-with-github-apps#authenticating-as-an-installation
        long installationId = options.CurrentValue.InstallationId;

        var credentials = await cache.GetOrCreateAsync($"github:installation-credentials:{installationId}", async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = TokenLifetime - TokenSkew;

            var accessToken = await client.GitHubApps.CreateInstallationToken(installationId);
            return new Credentials(accessToken.Token, AuthenticationType.Oauth);
        });

        return credentials!;
    }

    async Task<string> GraphQL.ICredentialStore.GetCredentials(CancellationToken cancellationToken)
    {
        var credentials = await GetCredentials();
        return credentials.GetToken();
    }
}
