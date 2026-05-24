// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubTokenBroker(
    SecretClient client,
    IOptionsMonitor<GitHubOptions> githubOptions,
    ILogger<GitHubTokenBroker> logger)
{
    public async Task<IResult> GetTokenAsync(string profileName, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        Log.ReceivedRequestWithOidcToken(logger, user);

        var options = githubOptions.CurrentValue.SecretBroker;
        var repository = user.FindFirstValue(GitHubOidcClaims.Repository) ?? string.Empty;

        if (!options.Repositories.TryGetValue(repository, out var profiles))
        {
            Log.RepositoryNotConfigured(logger, repository);
            return ForbiddenRepository(repository);
        }

        if (!profiles.TryGetValue(profileName, out var profile))
        {
            Log.ProfileNotConfigured(logger, profileName, repository);
            return Results.Problem($"Profile '{profileName}' not found.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!profile.IsAuthorized(user))
        {
            Log.ProfileNotAuthorized(logger, profileName, user);
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

            token = await GetTokenAsync(profile.TokenId, cancellationToken);
        }

        Log.IssuedGitHubToken(logger, profileName, user);

        return Results.Json(new() { Token = token }, AppJsonSerializerContext.Default.GitHubTokenResponse);

        static IResult ForbiddenRepository(string repository)
                => Results.Problem($"Repository '{repository}' is forbidden.", statusCode: StatusCodes.Status403Forbidden);
    }

    private async Task<string> GetTokenAsync(string tokenId, CancellationToken cancellationToken)
    {
        var secret = await client.GetSecretAsync(tokenId, cancellationToken: cancellationToken);
        return secret.Value.Value;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        public static void ReceivedRequestWithOidcToken(ILogger logger, ClaimsPrincipal user)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                var tokenId = user.FindFirstValue(JwtRegisteredClaimNames.Jti) ?? "unknown";

                Log.ReceivedRequestWithOidcToken(logger, tokenId, subject);
            }
        }

        public static void ProfileNotAuthorized(ILogger logger, string profile, ClaimsPrincipal user)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                Log.ProfileNotAuthorized(logger, profile, subject);
            }
        }

        public static void IssuedGitHubToken(ILogger logger, string profile, ClaimsPrincipal user)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                Log.IssuedGitHubToken(logger, profile, subject);
            }
        }

        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Received request with OIDC token with token ID {TokenId} for subject {Subject}.",
            SkipEnabledCheck = true)]
        public static partial void ReceivedRequestWithOidcToken(ILogger logger, string? tokenId, string? subject);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "No token profiles are configured for repository {Repository}.")]
        public static partial void RepositoryNotConfigured(ILogger logger, string repository);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Information,
            Message = "No profile named {Profile} is configured for repository {Repository}.")]
        public static partial void ProfileNotConfigured(ILogger logger, string profile, string repository);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Information,
            Message = "Profile {Profile} is not authorized for use for subject {Subject}.",
            SkipEnabledCheck = true)]
        public static partial void ProfileNotAuthorized(ILogger logger, string profile, string subject);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Information,
            Message = "Issued GitHub token for profile {Profile} for subject {Subject}.",
            SkipEnabledCheck = true)]
        public static partial void IssuedGitHubToken(ILogger logger, string profile, string subject);
    }
}
