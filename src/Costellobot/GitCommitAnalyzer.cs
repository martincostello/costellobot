// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using MartinCostello.Costellobot.Registries;
using Microsoft.Extensions.Options;
using Octokit;

namespace MartinCostello.Costellobot;

public sealed partial class GitCommitAnalyzer(
    IEnumerable<IPackageRegistry> registries,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<GitCommitAnalyzer> logger)
{
    private readonly ImmutableList<IPackageRegistry> _registries = registries.ToImmutableList();

    public static bool TryParseVersionNumber(
        string commitMessage,
        string dependencyName,
        [MaybeNullWhen(false)] out string? version)
    {
        // Extract the version numbers from the commit message.
        // See https://github.com/dependabot/fetch-metadata/blob/d9606730415777cc0dc46d64c4ce0e16624bd714/src/dependabot/update_metadata.ts#L30
        string escapedName = Regex.Escape(dependencyName).Replace("/", @"\/", StringComparison.Ordinal);
        string[] patterns =
        [
            $@"(Bumps|Updates) {escapedName} from (?<from>\d[^ ]*) to (?<to>\d[^ \n]*)\.", // Normal version updates
            $@"(Bumps|Updates) `{escapedName}` from (?<from>\d[^ ]*) to (?<to>\d[^ \n]*)\.?$", // Normal version updates with escaped name
            $@"(Bumps|Updates) \[{escapedName}\]\(.*\) from (?<from>\d[^ ]*) to (?<to>\d[^ \n]*)\.?$", // Normal version updates with link to repo
            $@"(Bumps|Updates) `?{escapedName}`? from \`(?<from>[\da-f][^ ]*)\` to \`(?<to>[\da-f][^ \n]*)\`\.?$", // Git submodule updates
            $@"(Bumps|Updates) \[{escapedName}\]\(.*\) from \`(?<from>[\da-f][^ ]*)\` to \`(?<to>[\da-f][^ \n]*)\`\.?", // Git submodule updates with link to repo
            $@"\[{escapedName}\]\(.*\)\.\s+\- \[Commits\]\(.*\/(?<from>\d[^ ]*)\.\.\.(?<to>\d[^ \n]*)\)", // Grouped version update that updates one package
        ];

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(commitMessage, pattern, RegexOptions.Multiline);
            var group = match.Groups["to"];

            if (group.Success)
            {
                version = group.Value.Trim().TrimEnd('.');
                return true;
            }
        }

        version = null;
        return false;
    }

    public async Task<bool> IsTrustedDependencyUpdateAsync(
        RepositoryId repository,
        string? reference,
        GitHubCommit commit)
    {
        return await IsTrustedDependencyUpdateAsync(
            repository,
            reference,
            commit.Sha,
            commit.Commit.Message);
    }

    public async Task<bool> IsTrustedDependencyUpdateAsync(
        RepositoryId repository,
        string? reference,
        string sha,
        string commitMessage)
    {
        bool isTrusted = IsTrustedDependencyName(
            repository,
            sha,
            commitMessage,
            out var dependencies);

        if (!isTrusted)
        {
            isTrusted = await IsTrustedDependencyOwnerAsync(
                repository,
                reference,
                commitMessage,
                dependencies);
        }

        if (isTrusted)
        {
            Log.TrustedDependenciesUpdated(
                logger,
                sha,
                repository,
                dependencies.Count);
        }

        return isTrusted;
    }

    private static List<string> ParseDependencies(string commitMessage)
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
        // Special case for use of martincostello/update-dotnet-sdk with dotnet-outdated to also update NuGet packages.
        // See https://github.com/martincostello/update-dotnet-sdk#advanced-example-workflow.
        if (reference?.StartsWith("update-dotnet-sdk-", StringComparison.Ordinal) == true)
        {
            return DependencyEcosystem.NuGet;
        }

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
        RepositoryId repository,
        string sha,
        string commitMessage,
        out IReadOnlyList<string> dependencies)
    {
        dependencies = ParseDependencies(commitMessage);

        var trustedDependencies = options.CurrentValue.TrustedEntities.Dependencies;

        if (dependencies.Count < 1 || trustedDependencies.Count < 1)
        {
            return false;
        }

        Log.CommitUpdatesDependencies(
            logger,
            sha,
            repository,
            [.. dependencies]);

        foreach (string dependency in dependencies)
        {
            if (!trustedDependencies.Any((p) => Regex.IsMatch(dependency, p)))
            {
                Log.UntrustedDependencyNameUpdated(
                    logger,
                    sha,
                    repository,
                    dependency);

                return false;
            }

            Log.TrustedDependencyNameUpdated(
                logger,
                sha,
                repository,
                dependency);
        }

        return true;
    }

    private async Task<bool> IsTrustedDependencyOwnerAsync(
        RepositoryId repository,
        string? reference,
        string commitMessage,
        IReadOnlyList<string> dependencies)
    {
        if (dependencies.Count < 1 || _registries.Count < 1)
        {
            // No dependencies were parsed or there are no applicable registries
            return false;
        }

        foreach (string dependency in dependencies)
        {
            if (!await IsTrustedDependencyOwnerAsync(
                    repository,
                    dependency,
                    reference,
                    commitMessage))
            {
                return false;
            }
        }

        return true;

        // If only one dependency was found, we can attempt to extract the version
        // from the commit message to see if the package was from a trusted publisher.
        async Task<bool> IsTrustedDependencyOwnerAsync(
            RepositoryId repository,
            string dependency,
            string? reference,
            string commitMessage)
        {
            if (!TryParseVersionNumber(commitMessage, dependency, out var version) ||
                string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            var ecosystem = ParseEcosystem(reference);

            if (ecosystem is DependencyEcosystem.Unknown or DependencyEcosystem.Unsupported)
            {
                return false;
            }

            if (!options.CurrentValue.TrustedEntities.Publishers.TryGetValue(ecosystem, out var publishers) ||
                publishers.Count < 1)
            {
                return false;
            }

            var registry = _registries.FirstOrDefault((p) => p.Ecosystem == ecosystem);

            if (registry is null)
            {
                return false;
            }

            try
            {
                var owners = await registry.GetPackageOwnersAsync(
                    repository,
                    dependency,
                    version);

                if (owners.Any(publishers.Contains))
                {
                    Log.TrustedDependencyOwnerUpdated(
                        logger,
                        reference,
                        repository,
                        dependency);

                    return true;
                }

                if (await registry.AreOwnersTrustedAsync(owners))
                {
                    Log.TrustedDependencyOwnerViaRegistryUpdated(
                        logger,
                        reference,
                        repository,
                        dependency,
                        registry.Ecosystem.ToString());

                    return true;
                }

                Log.UntrustedDependencyOwnerUpdated(
                    logger,
                    reference,
                    repository,
                    dependency);
            }
            catch (Exception ex)
            {
                Log.FailedToQueryPackageRegistry(logger, dependency, version, ecosystem, ex);
            }

            return false;
        }
    }

    [ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Commit {Sha} for {Repository} updates the following dependencies: {Dependencies}.")]
        public static partial void CommitUpdatesDependencies(
            ILogger logger,
            string sha,
            RepositoryId repository,
            string[] dependencies);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Debug,
           Message = "Commit {Sha} for {Repository} updates dependency {Dependency} which is not trusted by its name.")]
        public static partial void UntrustedDependencyNameUpdated(
            ILogger logger,
            string sha,
            RepositoryId repository,
            string dependency);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Information,
           Message = "Commit {Sha} for {Repository} updates {Count} trusted dependencies.")]
        public static partial void TrustedDependenciesUpdated(
            ILogger logger,
            string sha,
            RepositoryId repository,
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
           Level = LogLevel.Debug,
           Message = "Reference {Reference} for {Repository} updates dependency {Dependency} which is not trusted by its owner.")]
        public static partial void UntrustedDependencyOwnerUpdated(
            ILogger logger,
            string? reference,
            RepositoryId repository,
            string dependency);

        [LoggerMessage(
           EventId = 6,
           Level = LogLevel.Information,
           Message = "Commit {Sha} for {Repository} updates dependency {Dependency} which is trusted by its name.")]
        public static partial void TrustedDependencyNameUpdated(
            ILogger logger,
            string sha,
            RepositoryId repository,
            string dependency);

        [LoggerMessage(
           EventId = 7,
           Level = LogLevel.Information,
           Message = "Reference {Reference} for {Repository} updates dependency {Dependency} which is trusted through its owner.")]
        public static partial void TrustedDependencyOwnerUpdated(
            ILogger logger,
            string? reference,
            RepositoryId repository,
            string dependency);

        [LoggerMessage(
           EventId = 8,
           Level = LogLevel.Information,
           Message = "Reference {Reference} for {Repository} updates dependency {Dependency} whose owner is trusted by its registry {Registry}.")]
        public static partial void TrustedDependencyOwnerViaRegistryUpdated(
            ILogger logger,
            string? reference,
            RepositoryId repository,
            string dependency,
            string registry);
    }
}
