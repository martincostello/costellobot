// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubTokenBroker(
    SecretClient client,
    IOptionsMonitor<GitHubOptions> githubOptions,
    ILogger<GitHubTokenBroker> logger)
{
    public async Task<IResult> GetTokenAsync(string profileName, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
#pragma warning disable CA1873
            Log.ReceivedRequestWithOidcToken(logger, user.FindFirstValue("jti") ?? "unknown");
#pragma warning restore CA1873
        }

        var options = githubOptions.CurrentValue.SecretBroker;
        var repository = user.FindFirstValue(GitHubOidcClaims.Repository) ?? string.Empty;

        if (!options.Repositories.TryGetValue(repository, out var profiles))
        {
            return ForbiddenRepository(repository);
        }

        if (!profiles.TryGetValue(profileName, out var profile))
        {
            return Results.Problem($"Profile '{profileName}' not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!profile.IsAuthorized(user))
        {
            return Results.Problem($"Profile '{profileName}' is not authorized for use in this workflow run.", statusCode: StatusCodes.Status403Forbidden);
        }

        string token;

        if (!string.IsNullOrWhiteSpace(profile.AppId))
        {
            // TODO Get a token for the GitHub App installation instead of a secret from Key Vault
            return Results.Problem("GitHub App token exchange is not implemented.", statusCode: StatusCodes.Status501NotImplemented);
        }
        else
        {
            if (profile.TokenId is null || !options.Tokens.Contains(profile.TokenId))
            {
                return Results.Problem($"Profile '{profileName}' does not have a token configured.", statusCode: StatusCodes.Status404NotFound);
            }

            var secret = await client.GetSecretAsync(profile.TokenId, cancellationToken: cancellationToken);
            token = secret.Value.Value;
        }

        return Results.Json(new() { Token = token }, AppJsonSerializerContext.Default.GitHubTokenResponse);

        static IResult ForbiddenRepository(string repository)
                => Results.Problem($"Repository '{repository}' is forbidden.", statusCode: StatusCodes.Status403Forbidden);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Received request with OIDC token with token ID {TokenId}.",
            SkipEnabledCheck = true)]
        public static partial void ReceivedRequestWithOidcToken(ILogger logger, string? tokenId);
    }
}
