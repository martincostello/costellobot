// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using MartinCostello.Costellobot.Registries;
using Microsoft.Extensions.Options;
using Octokit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MartinCostello.Costellobot;

public sealed partial class GitCommitAnalyzer(
    IGitHubClientForInstallation client,
    IEnumerable<IPackageRegistry> registries,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<GitCommitAnalyzer> logger)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .Build();

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
        GitHubCommit commit,
        string? diff)
    {
        return await IsTrustedDependencyUpdateAsync(
            repository,
            reference,
            commit.Sha,
            commit.Commit.Message,
            diff);
    }

    public async Task<bool> IsTrustedDependencyUpdateAsync(
        RepositoryId repository,
        string? reference,
        string sha,
        string commitMessage,
        string? diff)
    {
        var ecosystem = ParseEcosystem(reference);

        if (ecosystem is DependencyEcosystem.Unknown or DependencyEcosystem.Unsupported)
        {
            return false;
        }

        var dependencies = await GetDependencyTrustAsync(repository, reference, sha, commitMessage, diff, ecosystem);

        bool isTrusted = dependencies.Count > 0 && dependencies.Values.All((p) => p);

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

        var dependencies = new HashSet<string>(commitLines.Length);

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

        return [.. dependencies];
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
            "submodules" => DependencyEcosystem.GitSubmodule,
            _ => DependencyEcosystem.Unsupported,
        };
    }

    private async Task<Dictionary<string, bool>> GetDependencyTrustAsync(
        RepositoryId repository,
        string? reference,
        string sha,
        string commitMessage,
        string? diff,
        DependencyEcosystem ecosystem)
    {
        var dependencyNames = ParseDependencies(commitMessage);

        if (dependencyNames.Count < 1)
        {
            return [];
        }

        Log.CommitUpdatesDependencies(
            logger,
            sha,
            repository,
            [.. dependencyNames]);

        // First do a simple lookup by name
        var trustedDependencies = options.CurrentValue.TrustedEntities.Dependencies;
        var dependencyTrust = dependencyNames.ToDictionary((k) => k, (_) => false);

        var dependenciesToIgnore = await GetIgnoredDependenciesAsync(repository, reference, ecosystem);
        var ignoredDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string dependency in dependencyNames)
        {
            bool ignored = dependenciesToIgnore.Any((p) => Regex.IsMatch(dependency, p, RegexOptions.None, RegexTimeout));

            if (ignored)
            {
                ignoredDependencies.Add(dependency);
            }

            if (!ignored &&
                trustedDependencies.Any((p) => Regex.IsMatch(dependency, p, RegexOptions.None, RegexTimeout)))
            {
                Log.TrustedDependencyNameUpdated(
                    logger,
                    sha,
                    repository,
                    dependency);

                dependencyTrust[dependency] = true;
            }
            else
            {
                Log.UntrustedDependencyNameUpdated(
                    logger,
                    sha,
                    repository,
                    dependency);
            }
        }

        // If a dependency is not trusted by name alone, determine whether it is trusted through its owner
        foreach (string dependency in dependencyTrust.Where((p) => !p.Value).Select((p) => p.Key))
        {
            if (ignoredDependencies.Contains(dependency))
            {
                // We only care if all dependencies are trusted, so stop looking on the first ignore
                break;
            }

            if (!await IsTrustedDependencyOwnerAsync(
                    repository,
                    dependency,
                    reference,
                    commitMessage,
                    diff))
            {
                // We only care if all dependencies are trusted, so stop looking on the first failure
                break;
            }

            dependencyTrust[dependency] = true;
        }

        return dependencyTrust;

        // If only one dependency was found, we can attempt to extract the version
        // from the commit message to see if the package was from a trusted publisher.
        async Task<bool> IsTrustedDependencyOwnerAsync(
            RepositoryId repository,
            string dependency,
            string? reference,
            string commitMessage,
            string? diff)
        {
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

            // HACK https://github.com/dependabot/dependabot-core/issues/8217
            // Use the Git diff for the commit and try to parse it to get
            // the version of the dependency that was updated if it could not
            // otherwise be determined.
            if (!TryParseVersionNumber(commitMessage, dependency, out var version) &&
                ecosystem is DependencyEcosystem.NuGet &&
                !string.IsNullOrEmpty(diff) &&
                GitDiffParser.TryParseUpdatedPackages(diff, out var updates) &&
                updates.TryGetValue(dependency, out var update))
            {
                version = update.To.ToString();
            }

            if (string.IsNullOrWhiteSpace(version))
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

    private async Task<IReadOnlyList<string>> GetIgnoredDependenciesAsync(
        RepositoryId repository,
        string? reference,
        DependencyEcosystem ecosystem)
    {
        IReadOnlyList<string> dependencies = [];

        try
        {
            var configuration = await client.Repository.Content.GetRawContentByRef(
                repository.Owner,
                repository.Name,
                ".github/dependabot.yml",
                reference);

            using var stream = new MemoryStream(configuration);
            using var reader = new StreamReader(stream);

            var config = YamlDeserializer.Deserialize<DependabotConfig>(reader);

            if (config?.Version is 2 && config.Updates is not null)
            {
                var comparison = StringComparison.OrdinalIgnoreCase;
                var ecosystemName = ecosystem.ToString();
                var normalized = ecosystemName.Replace("-", string.Empty, comparison);

                dependencies = config.Updates
                    .Where((p) => string.Equals(p.PackageEcosystem, normalized, StringComparison.OrdinalIgnoreCase))
                    .SelectMany((p) => p.Ignore)
                    .Select((p) => p.DependencyName)
                    .ToList();
            }
        }
        catch (NotFoundException)
        {
            // No dependabot configuration file
        }
        catch (Exception ex)
        {
            Log.FailedToParseDependabotConfiguration(logger, repository, ex);
        }

        return dependencies;
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

        [LoggerMessage(
           EventId = 9,
           Level = LogLevel.Warning,
           Message = "Failed to parse dependabot configuration for repository {Repository}.")]
        public static partial void FailedToParseDependabotConfiguration(
            ILogger logger,
            RepositoryId repository,
            Exception exception);
    }

    private sealed class DependabotConfig
    {
        [YamlMember(Alias = "version")]
        public long Version { get; set; }

        [YamlMember(Alias = "updates")]
        public IList<Update> Updates { get; set; } = [];
    }

    private sealed class Update
    {
        [YamlMember(Alias = "package-ecosystem")]
        public string? PackageEcosystem { get; set; }

        [YamlMember(Alias = "ignore")]
        public IList<Ignore> Ignore { get; set; } = [];
    }

    private sealed class Ignore
    {
        [YamlMember(Alias = "dependency-name")]
        public string DependencyName { get; set; } = string.Empty;
    }
}
