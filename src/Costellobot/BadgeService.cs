// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Octokit;

namespace MartinCostello.Costellobot;

public sealed partial class BadgeService(
    IGitHubClientForInstallation client,
    IOptionsMonitor<GitHubOptions> options,
    ILogger<BadgeService> logger)
{
    private static readonly string[] AlertTypes = ["code-scanning", "dependabot", "secret-scanning"];

    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.CurrentValue.BadgesKey);

    public async Task<string?> GetBadgeAsync(string type, string owner, string repo, string? signature)
    {
        if (!VerifySignature(type, owner, repo, signature))
        {
            return null;
        }

        try
        {
            return type.ToUpperInvariant() switch
            {
                "RELEASE" => await LatestReleaseBadgeAsync(owner, repo),
                "SECURITY" => await SecurityAlertsAsync(owner, repo),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            Log.FailedToGenerateBadge(logger, ex, type, owner, repo);
            return null;
        }
    }

    private bool VerifySignature(string type, string owner, string repo, string? signature)
    {
        const int Length = 256 / 8;
        Span<byte> expected = stackalloc byte[Length];

        if (string.IsNullOrEmpty(signature) || !Convert.TryFromBase64String(signature, expected, out var written) || written != Length)
        {
            return false;
        }

        var data = GetBytes($"badge-{type}-{owner}-{repo}");
        var actual = HMACSHA256.HashData(_key, data);

        return CryptographicOperations.FixedTimeEquals(expected, actual);

        static ReadOnlySpan<byte> GetBytes(string value)
            => Encoding.UTF8.GetBytes(value);
    }

    private async Task<string?> LatestReleaseBadgeAsync(string owner, string name)
    {
        Release? release = null;

        try
        {
            release = await client.Repository.Release.GetLatest(owner, name);
        }
        catch (NotFoundException)
        {
            // No releases, or all releases are pre-releases
        }

        if (release is null)
        {
            var releases = await client.Repository.Release.GetAll(owner, name, new() { PageSize = 1 });

            if (releases.Count < 1)
            {
                return null;
            }

            release = releases[0];
        }

        string releaseName = release.Name;

        if (releaseName[0] is not 'v' && !char.IsAsciiDigit(releaseName[0]))
        {
            releaseName = releaseName.Split(' ').Last();
        }

        releaseName = Uri.EscapeDataString(releaseName).Replace("-", "--", StringComparison.Ordinal);

        return $"https://img.shields.io/badge/release-{releaseName}-blue?logo=github";
    }

    private async Task<string?> SecurityAlertsAsync(string owner, string name)
    {
        int count = 0;

        foreach (var type in AlertTypes)
        {
            count += await GetAlertsAsync(client, owner, name, type);
        }

        string color = count is 0 ? "brightgreen" : "red";

        return $"https://img.shields.io/badge/security-{count}-{color}?logo=github";

        static async Task<int> GetAlertsAsync(IGitHubClient client, string owner, string name, string type)
        {
            var parameters = new Dictionary<string, string>(1)
            {
                ["state"] = "open",
            };

            try
            {
                if (await client.Connection.Get<object[]>(new($"/repos/{owner}/{name}/{type}/alerts", UriKind.Relative), parameters) is { Body.Length: > 0 } alerts)
                {
                    return alerts.Body.Length;
                }
            }
            catch (Exception ex) when (ex is ForbiddenException or NotFoundException)
            {
                // Not enabled or no access
            }

            return 0;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Warning,
           Message = "Failed to generate badge of type {BadgeType} for {Owner}/{Repository}.")]
        public static partial void FailedToGenerateBadge(
            ILogger logger,
            Exception exception,
            string badgeType,
            string owner,
            string repository);
    }
}
