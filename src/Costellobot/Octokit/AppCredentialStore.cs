// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using MartinCostello.Costellobot;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Octokit;

public sealed class AppCredentialStore(
    HybridCache cache,
    CryptoProviderFactory cryptoProviderFactory,
    TimeProvider timeProvider,
    IOptionsMonitor<GitHubOptions> options) : CredentialStore
{
    private readonly IOptionsMonitor<GitHubOptions> _options = options;

    public override async Task<Credentials> GetCredentials()
    {
        var token = await cache.GetOrCreateAsync(
            "github:app-credentials",
            this,
            static (self, _) => ValueTask.FromResult(self.CreateJwtForApp()),
            CacheEntryOptions,
            CacheTags);

        return new Credentials(token, AuthenticationType.Bearer);
    }

    private string CreateJwtForApp()
    {
        // See https://docs.github.com/en/developers/apps/building-github-apps/authenticating-with-github-apps#authenticating-as-a-github-app
        var options = _options.CurrentValue;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        using var algorithm = RSA.Create();
        algorithm.ImportFromPem(options.PrivateKey);

        var tokenDescriptor = new SecurityTokenDescriptor()
        {
            Expires = utcNow.Add(TokenLifetime),
            IssuedAt = utcNow.Add(-TokenSkew),
            Issuer = options.AppId,
            SigningCredentials = CreateSigningCredentials(algorithm),
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(tokenDescriptor);
    }

    private SigningCredentials CreateSigningCredentials(RSA algorithm)
    {
        var key = new RsaSecurityKey(algorithm);

        // Use a custom CryptoProviderFactory so that keys are not cached and then disposed of, see below:
        // https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/issues/1302
        return new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = cryptoProviderFactory,
        };
    }
}
