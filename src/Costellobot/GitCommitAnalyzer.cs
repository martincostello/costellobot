// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using MartinCostello.Costellobot.Registries;
using Microsoft.Extensions.Options;
using Octokit;

namespace MartinCostello.Costellobot;

public sealed partial class GitCommitAnalyzer
{
    private readonly IReadOnlyCollection<IPackageRegistry> _registries;
    private readonly IOptionsMonitor<WebhookOptions> _options;
    private readonly ILogger _logger;

    public GitCommitAnalyzer(
        IEnumerable<IPackageRegistry> registries,
        IOptionsMonitor<WebhookOptions> options,
        ILogger<GitCommitAnalyzer> logger)
    {
        _registries = registries.ToArray();
        _options = options;
        _logger = logger;
    }

    public static bool TryParseVersionNumber(string commitMessage, [MaybeNullWhen(false)] out string? version)
    {
        // Extract the version numbers from the commit message.
        // See https://github.com/dependabot/fetch-metadata/blob/d9606730415777cc0dc46d64c4ce0e16624bd714/src/dependabot/update_metadata.ts#L30
        string[] patterns = new[]
        {
            @"Bumps .* from (?<from>\d[^ ]*) to (?<to>\d[^ ]*)\.", // Normal version updates
            @"Bumps .* from \`(?<from>[\da-f][^ ]*)\` to \`(?<to>[\da-f][^ ]*)\`\.", // Git submodule updates
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(commitMessage, pattern, RegexOptions.Multiline);
            var group = match.Groups["to"];

            if (group.Success)
            {
                version = group.Value;
                return true;
            }
        }

        version = null;
        return false;
    }

    public async Task<bool> IsTrustedDependencyUpdateAsync(
        string owner,
        string name,
        string? reference,
        GitHubCommit commit)
    {
        return await IsTrustedDependencyUpdateAsync(
            owner,
            name,
            reference,
            commit.Sha,
            commit.Commit.Message);
    }

    public async Task<bool> IsTrustedDependencyUpdateAsync(
        string owner,
        string name,
        string? reference,
        string sha,
        string commitMessage)
    {
        bool isTrusted = IsTrustedDependencyName(
            owner,
            name,
            sha,
            commitMessage,
            out var dependencies);

        if (!isTrusted)
        {
            isTrusted = await IsTrustedDependencyOwnerAsync(
                owner,
                name,
                reference,
                commitMessage,
                dependencies);
        }

        if (isTrusted)
        {
            Log.TrustedDependenciesUpdated(
                _logger,
                sha,
                owner,
                name,
                dependencies.Count);
        }

        return isTrusted;
    }

    private static IReadOnlyList<string> ParseDependencies(string commitMessage)
    {
        string[] commitLines = commitMessage
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var dependencies = new List<string>(commitLines.Length);

        foreach (string line in commitLines)
        {
            const string Prefix = "- dependency-name: ";

            if (line.StartsWith(Prefix, StringComparison.Ordinal))
            {
                string dependencyName = line[Prefix.Length..];

                if (!string.IsNullOrEmpty(dependencyName))
                {
                    // Sometimes the dependencies are wrapped with quotes,
                    // for example "- dependency-name: "@actions/github"".
                    string trimmed = dependencyName
                        .Trim()
                        .Trim('"');

                    dependencies.Add(trimmed);
                }
            }
        }

        dependencies.TrimExcess();

        return dependencies;
    }

    private static DependencyEcosystem ParseEcosystem(string? reference)
    {
        // Approach based on what Dependabot itself does to parse commits to generate release notes.
        // See https://github.com/dependabot/fetch-metadata/blob/d9606730415777cc0dc46d64c4ce0e16624bd714/src/dependabot/update_metadata.ts#L55.
        // Example ref (branch) names:
        // * dependabot/github_actions/actions/dependency-review-action-2
        // * dependabot/npm_and_yarn/src/Costellobot/typescript-eslint/eslint-plugin-5.31.0
        // * dependabot/nuget/Microsoft.IdentityModel.JsonWebTokens-6.22.0
        string[]? parts = reference?.Split('/');

        if (parts is null ||
            parts.Length < 3 ||
            !string.Equals(parts[0], "dependabot", StringComparison.Ordinal))
        {
            return DependencyEcosystem.Unknown;
        }

        return parts[1] switch
        {
            "github_actions" => DependencyEcosystem.GitHubActions,
            "npm_and_yarn" => DependencyEcosystem.Npm,
            "nuget" => DependencyEcosystem.NuGet,
            "submodules" => DependencyEcosystem.Submodules,
            _ => DependencyEcosystem.Unsupported,
        };
    }

    private bool IsTrustedDependencyName(
        string owner,
        string name,
        string sha,
        string commitMessage,
        out IReadOnlyList<string> dependencies)
    {
        dependencies = ParseDependencies(commitMessage);

        var trustedDependencies = _options.CurrentValue.TrustedEntities.Dependencies;

        if (dependencies.Count < 1 || trustedDependencies.Count < 1)
        {
            return false;
        }

        Log.CommitUpdatesDependencies(
            _logger,
            sha,
            owner,
            name,
            dependencies.ToArray());

        foreach (string dependency in dependencies)
        {
            if (!trustedDependencies.Any((p) => Regex.IsMatch(dependency, p)))
            {
                Log.UntrustedDependencyNameUpdated(
                    _logger,
                    sha,
                    owner,
                    name,
                    dependency);

                return false;
            }
        }

        return true;
    }

    private async Task<bool> IsTrustedDependencyOwnerAsync(
        string owner,
        string name,
        string? reference,
        string commitMessage,
        IReadOnlyList<string> dependencies)
    {
        if (dependencies.Count != 1 || _registries.Count < 1)
        {
            // Either no dependencies were parsed, or more than one was found,
            // in which case we're not able to look up the version for every one.
            return false;
        }

        // If only one dependency was found, we can attempt to extract the version
        // from the commit message to see if the package was from a trusted publisher.
        string dependency = dependencies[0];

        if (!TryParseVersionNumber(commitMessage, out var version) ||
            string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var ecosystem = ParseEcosystem(reference);

        if (ecosystem == DependencyEcosystem.Unknown ||
            ecosystem == DependencyEcosystem.Unsupported)
        {
            return false;
        }

        if (!_options.CurrentValue.TrustedEntities.Publishers.TryGetValue(ecosystem, out var publishers) ||
            publishers.Count < 1)
        {
            return false;
        }

        var registry = _registries
            .Where((p) => p.Ecosystem == ecosystem)
            .FirstOrDefault();

        if (registry is null)
        {
            return false;
        }

        try
        {
            var owners = await registry.GetPackageOwnersAsync(
                owner,
                name,
                dependency,
                version);

            if (owners.Any((p) => publishers.Contains(p)))
            {
                return true;
            }

            Log.UntrustedDependencyOwnerUpdated(
                _logger,
                reference,
                owner,
                name,
                dependency);
        }
        catch (Exception ex)
        {
            Log.FailedToQueryPackageRegistry(_logger, dependency, version, ecosystem, ex);
        }

        return false;
    }

    [ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Commit {Sha} for {Owner}/{Repository} updates the following dependencies: {Dependencies}.")]
        public static partial void CommitUpdatesDependencies(
            ILogger logger,
            string sha,
            string owner,
            string repository,
            string[] dependencies);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Information,
           Message = "Commit {Sha} for {Owner}/{Repository} updates dependency {Dependency} which is not trusted by its name.")]
        public static partial void UntrustedDependencyNameUpdated(
            ILogger logger,
            string sha,
            string owner,
            string repository,
            string dependency);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Information,
           Message = "Commit {Sha} for {Owner}/{Repository} updates {Count} trusted dependencies.")]
        public static partial void TrustedDependenciesUpdated(
            ILogger logger,
            string sha,
            string owner,
            string repository,
            int count);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Error,
           Message = "Failed to query owners for package {PackageId} version {PackageVersion} from the {Ecosystem} ecosystem.")]
        public static partial void FailedToQueryPackageRegistry(
            ILogger logger,
            string packageId,
            string packageVersion,
            DependencyEcosystem ecosystem,
            Exception exception);

        [LoggerMessage(
           EventId = 5,
           Level = LogLevel.Information,
           Message = "Reference {Reference} for {Owner}/{Repository} updates dependency {Dependency} which is not trusted by its owner.")]
        public static partial void UntrustedDependencyOwnerUpdated(
            ILogger logger,
            string? reference,
            string owner,
            string repository,
            string dependency);
    }
}
