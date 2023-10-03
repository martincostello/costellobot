// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using MartinCostello.Costellobot;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Octokit;

public sealed class AppCredentialStore(
    IMemoryCache cache,
    CryptoProviderFactory cryptoProviderFactory,
    TimeProvider timeProvider,
    IOptionsMonitor<GitHubOptions> options) : ICredentialStore
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TokenSkew = TimeSpan.FromMinutes(1);
    private readonly IOptionsMonitor<GitHubOptions> _options = options;

    public Task<Credentials> GetCredentials()
    {
        var credentials = cache.GetOrCreate("github:app-credentials", (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = TokenLifetime - TokenSkew;
            return CreateJwtForApp();
        });

        return Task.FromResult(credentials!);
    }

    private Credentials CreateJwtForApp()
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
        var appToken = handler.CreateToken(tokenDescriptor);

        return new Credentials(appToken, AuthenticationType.Bearer);
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
