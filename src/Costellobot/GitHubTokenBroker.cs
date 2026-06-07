// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Security.Claims;
using Azure.Security.KeyVault.Secrets;
using MartinCostello.Costellobot.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Octokit;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubTokenBroker(
    SecretClient client,
    GitHubTokenProfileAuthorizer authorizer,
    IGitHubClientFactory clientFactory,
    CostellobotMetrics metrics,
    IOptionsMonitor<GitHubOptions> monitor,
    ILogger<GitHubTokenBroker> logger)
{
    private static readonly FrozenSet<string> AllRepositories = FrozenSet.Create(["*"]);

    public async Task<IResult> GetTokenAsync(string profileName, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        Log.ReceivedRequestWithOidcToken(logger, user);

        var github = monitor.CurrentValue;
        var options = github.TokenBroker;
        var repository = user.FindFirstValue(GitHubOidcClaims.Repository) ?? string.Empty;

        if (!options.IsEnabled)
        {
            return Results.Problem("GitHub token broker is not enabled.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!options.Repositories.TryGetValue(repository, out var profiles))
        {
            Log.RepositoryNotConfigured(logger, repository);
            return Results.Problem($"Repository '{repository}' is forbidden.", statusCode: StatusCodes.Status403Forbidden);
        }

        if (!profiles.TryGetValue(profileName, out var profile))
        {
            Log.ProfileNotConfigured(logger, profileName, repository);
            return Results.Problem($"Profile '{profileName}' not found.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!authorizer.IsAuthorized(user, profile, repository))
        {
            Log.ProfileNotAuthorized(logger, profileName, user);
            return Results.Problem($"Profile '{profileName}' is not authorized for use in this workflow run.", statusCode: StatusCodes.Status403Forbidden);
        }

        GitHubTokenResponse response;

        if (!string.IsNullOrWhiteSpace(profile.AppId))
        {
            if (!github.Apps.TryGetValue(profile.AppId, out var app))
            {
                return Results.Problem($"Profile '{profileName}' does not have a valid GitHub app configured.", statusCode: StatusCodes.Status404NotFound);
            }

            var parts = repository.Split('/');
            var owner = parts[0];

            string repo;
            IList<string>? targetRepositories;

            if (profile.TargetRepositories is not { Count: > 0 } targets)
            {
                repo = parts[1];
                targetRepositories = [repo];
            }
            else if (targets.SequenceEqual(AllRepositories, StringComparer.Ordinal))
            {
                repo = parts[1];
                targetRepositories = null;
            }
            else
            {
                repo = profile.TargetRepositories[0];
                targetRepositories = profile.TargetRepositories;
            }

            var client = clientFactory.CreateForApp(profile.AppId);
            var installation = await client.GitHubApps.GetRepositoryInstallationForCurrent(owner, repo);
            var accessToken = await client.CreateInstallationTokenAsync(installation.Id, targetRepositories, profile.AppPermissions, cancellationToken);

            response = new()
            {
                AppId = long.Parse(app.AppId, CultureInfo.InvariantCulture),
                AppSlug = app.Name,
                Token = accessToken.Token,
                TokenType = "app",
            };
        }
        else
        {
            if (profile.TokenId is null || !options.Tokens.Contains(profile.TokenId))
            {
                return Results.Problem($"Profile '{profileName}' does not have a token configured.", statusCode: StatusCodes.Status404NotFound);
            }

            var token = await GetTokenAsync(profile.TokenId, cancellationToken);

            response = new()
            {
                Token = token,
                TokenType = "user",
            };
        }

        Log.IssuedGitHubToken(logger, profileName, user);
        metrics.TokenIssued(repository, profileName);

        return Results.Json(response, AppJsonSerializerContext.Default.GitHubTokenResponse);
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

            if (logger.IsEnabled(LogLevel.Debug))
            {
                var claims = string.Join(Environment.NewLine, user.Claims.Select(c => $"{c.Type}={c.Value}"));
                Log.ReceivedRequestWithOidcTokenWithClaims(logger, claims);
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

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Debug,
            Message = "Received request with OIDC token with claims: {Claims}.",
            SkipEnabledCheck = true)]
        public static partial void ReceivedRequestWithOidcTokenWithClaims(ILogger logger, string claims);
    }
}
