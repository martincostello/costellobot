// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using MartinCostello.Costellobot.Registries;
using NuGet.Versioning;
using Octokit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using DependencyUpdate = (string Name, string? Version, string? UpdateType);

namespace MartinCostello.Costellobot;

public sealed partial class GitCommitAnalyzer(
    GitHubWebhookContext context,
    IEnumerable<IPackageRegistry> registries,
    ITrustStore trustStore,
    ILogger<GitCommitAnalyzer> logger)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .Build();

    private readonly ImmutableList<IPackageRegistry> _registries = [.. registries];

    public static bool TryParseVersionNumber(
        string commitMessage,
        string dependencyName,
        [NotNullWhen(true)] out string? version)
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

    public async Task<(DependencyEcosystem Ecosystem, IDictionary<string, (bool Trusted, string? Version)> Dependencies)> GetDependencyTrustAsync(
        RepositoryId repository,
        string? reference,
        GitHubCommit commit,
        string? diff)
    {
        return await GetDependencyTrustAsync(
            repository,
            reference,
            commit.Sha,
            commit.Commit.Message,
            diff);
    }

    public async Task<(DependencyEcosystem Ecosystem, IDictionary<string, (bool Trusted, string? Version)> Dependencies)> GetDependencyTrustAsync(
        RepositoryId repository,
        string? reference,
        string sha,
        string commitMessage,
        string? diff)
    {
        var ecosystem = ParseEcosystem(reference);

        if (ecosystem is DependencyEcosystem.Unknown or DependencyEcosystem.Unsupported)
        {
            return (ecosystem, new Dictionary<string, (bool Trusted, string? Version)>(0));
        }

        var dependencies = await GetDependencyTrustAsync(
            repository,
            reference,
            sha,
            commitMessage,
            diff,
            ecosystem,
            stopAfterFirstUntrustedDependency: false);

        return (ecosystem, dependencies);
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

        var dependencies = await GetDependencyTrustAsync(
            repository,
            reference,
            sha,
            commitMessage,
            diff,
            ecosystem,
            stopAfterFirstUntrustedDependency: true);

        bool isTrusted = dependencies.Count > 0 && dependencies.Values.All((p) => p.Trusted);

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

    private static string? GetUpdateType(string from, string to)
    {
        string? updateType = null;

        if (NuGetVersion.TryParse(from, out var versionFrom) &&
            NuGetVersion.TryParse(to, out var versionTo))
        {
            if (versionFrom.Major < versionTo.Major)
            {
                updateType = "version-update:semver-major";
            }
            else if (versionFrom.Minor < versionTo.Minor)
            {
                updateType = "version-update:semver-minor";
            }
            else
            {
                updateType = "version-update:semver-patch";
            }
        }

        return updateType;
    }

    private static List<DependencyUpdate> ParseDependencies(string commitMessage)
    {
        string[] commitLines = commitMessage
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var updates = new HashSet<DependencyUpdate>();

        // Look for dependabot YAML metadata in the commit message
        int start = Array.IndexOf(commitLines, "---");
        int end = Array.IndexOf(commitLines, "...");

        if (start > -1 && ((end - start) > 1))
        {
            var yaml = string.Join('\n', commitLines[(start + 1)..(end - 1)]);
            ParseDependabotYaml(yaml, updates);
        }
        else
        {
            // Look for renovate Markdown metadata in the commit message.
            start = commitMessage.IndexOf('|', StringComparison.Ordinal);
            end = commitMessage.LastIndexOf('|');

            if (start > -1 && ((end - start) > 1))
            {
                var markdown = commitMessage.Substring(start, end - start + 1);
                ParseRenovateMarkdown(markdown, updates);
            }
        }

        return [.. updates];

        static void ParseDependabotYaml(string yaml, HashSet<DependencyUpdate> updates)
        {
            using var reader = new StringReader(yaml);

            var metadata = YamlDeserializer.Deserialize<DependabotMetadata>(reader);

            if (metadata.Dependencies is { Count: > 0 } dependencies)
            {
                foreach (var dependency in dependencies)
                {
                    if (dependency?.DependencyName is { Length: > 0 } name)
                    {
                        updates.Add((name, dependency.DependencyVersion, dependency.UpdateType));
                    }
                }
            }
        }

        static void ParseRenovateMarkdown(string markdown, HashSet<DependencyUpdate> updates)
        {
            var pipeline = new MarkdownPipelineBuilder()
                .UsePipeTables()
                .Build();

            var document = Markdown.Parse(markdown, pipeline);

            // Find the first table in the document
            if (document.Descendants<Table>().FirstOrDefault() is { } table)
            {
                // | datasource | package                   | from  | to    |
                // | ---------- | ------------------------- | ----- | ----- |
                // | nuget      | xunit.runner.visualstudio | 3.1.0 | 3.1.1 |
                // | nuget      | xunit.v3                  | 2.0.2 | 2.0.3 |
                foreach (var row in table.Descendants<TableRow>().Where((p) => !p.IsHeader))
                {
                    if (row.Descendants<TableCell>().ToArray() is not { Length: 4 } cells)
                    {
                        continue;
                    }

                    if (GetCellValue(cells, 1, markdown) is { Length: > 0 } name)
                    {
                        string from = GetCellValue(cells, 2, markdown).TrimStart('v');
                        string to = GetCellValue(cells, 3, markdown).TrimStart('v');

                        string? updateType = GetUpdateType(from, to);

                        updates.Add((name, to, updateType));
                    }
                }
            }
        }

        static string GetCellValue(TableCell[] cells, int index, string markdown)
        {
            var cell = cells[index];
            var range = new System.Range(cell.Span.Start, cell.Span.End);

            return markdown[range].Trim();
        }
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
        string[] parts = reference?.Split('/') ?? [];

        if (parts is null ||
            parts.Length < 3 ||
            !(string.Equals(parts[0], "dependabot", StringComparison.Ordinal) ||
              string.Equals(parts[0], "renovate", StringComparison.Ordinal)))
        {
            return DependencyEcosystem.Unknown;
        }

        return parts[1] switch
        {
            "bundler" => DependencyEcosystem.Ruby,
            "docker" or "dockerfile" or "docker-compose" => DependencyEcosystem.Docker,
            "github-actions" or "github_actions" => DependencyEcosystem.GitHubActions,
            "npm" or "npm_and_yarn" => DependencyEcosystem.Npm,
            "nuget" => DependencyEcosystem.NuGet,
            "git-submodules" or "submodules" => DependencyEcosystem.GitSubmodule,
            _ => DependencyEcosystem.Unsupported,
        };
    }

    private static string? TryGetDependencyVersion(
        DependencyEcosystem ecosystem,
        string dependency,
        string commitMessage,
        string? diff)
    {
        // See https://github.com/dependabot/dependabot-core/issues/8217.
        // Use the Git diff for the commit and try to parse it to get
        // the version of the dependency that was updated if it could not
        // otherwise be determined.
        if (!TryParseVersionNumber(commitMessage, dependency, out var version) &&
            ecosystem is DependencyEcosystem.NuGet or DependencyEcosystem.Npm &&
            !string.IsNullOrEmpty(diff) &&
            GitDiffParser.TryParseUpdatedPackages(diff, out var updates) &&
            updates.TryGetValue(dependency, out var update))
        {
            version = update.To;
        }

        return version;
    }

    private async Task<Dictionary<string, (bool Trusted, string? Version)>> GetDependencyTrustAsync(
        RepositoryId repository,
        string? reference,
        string sha,
        string commitMessage,
        string? diff,
        DependencyEcosystem ecosystem,
        bool stopAfterFirstUntrustedDependency)
    {
        var dependencies = ParseDependencies(commitMessage);

        // Renovate updates for just the digest do not contain any commit metadata
        if (dependencies.Count < 1 &&
            diff is not null &&
            ecosystem is DependencyEcosystem.Docker &&
            GitDiffParser.TryParseUpdatedPackages(diff, out var updates))
        {
            dependencies = [.. updates.Select((p) => new DependencyUpdate(p.Key, p.Value.To, GetUpdateType(p.Value.From, p.Value.To)))];
        }

        if (dependencies.Count < 1)
        {
            return [];
        }

#pragma warning disable CA1873
        if (logger.IsEnabled(LogLevel.Information))
        {
            Log.CommitUpdatesDependencies(
                logger,
                sha,
                repository,
                [.. dependencies.Select((p) => p.Name)]);
        }
#pragma warning restore CA1873

        // First do a simple lookup by name
        var trustedDependencies = context.WebhookOptions.TrustedEntities.Dependencies;

        Dictionary<string, (bool Trusted, string? Version)> dependencyTrust = new(dependencies.Count);

        foreach (var dependency in dependencies)
        {
            dependencyTrust[dependency.Name] = (false, dependency.Version);
        }

        var dependenciesToIgnore = await GetIgnoredDependenciesAsync(repository, reference, ecosystem);
        var ignoredDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach ((string dependency, string? version, string? updateType) in dependencies)
        {
            bool ignored = dependenciesToIgnore
                .Any((p) =>
                    Regex.IsMatch(dependency, p.Name, RegexOptions.None, RegexTimeout) &&
                    (p.UpdateType is null || string.Equals(updateType, p.UpdateType, StringComparison.Ordinal)));

            if (ignored)
            {
                Log.UpdateToDependencyIsIgnoredByDependabot(logger, reference, repository, dependency);
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

                dependencyTrust[dependency] = (true, version);
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

        // If a dependency is not trusted by name alone, determine whether it is trusted through its owner or implicitly
        foreach ((string dependency, var trust) in dependencyTrust.Where((p) => !p.Value.Trusted))
        {
            string? version = trust.Version;

            if (ignoredDependencies.Contains(dependency))
            {
                // We only care if all dependencies are trusted, so stop looking on the first ignore
                if (stopAfterFirstUntrustedDependency)
                {
                    break;
                }
            }
            else
            {
                (bool trusted, version) = await IsTrustedByVersionOrOwnerAsync(
                    repository,
                    dependency,
                    version,
                    reference,
                    commitMessage,
                    diff);

                dependencyTrust[dependency] = (trusted, version);

                if (!trusted && stopAfterFirstUntrustedDependency)
                {
                    // We only care if all dependencies are trusted, so stop looking on the first failure
                    break;
                }
            }
        }

        return dependencyTrust;

        // If only one dependency was found, we can attempt to extract the version
        // from the commit message to see if the package was from a trusted publisher
        // or was trusted implicitly through prior approval of this specific version.
        async Task<(bool IsTrusted, string? Version)> IsTrustedByVersionOrOwnerAsync(
            RepositoryId repository,
            string dependency,
            string? version,
            string? reference,
            string commitMessage,
            string? diff)
        {
            if (ecosystem is DependencyEcosystem.Unknown or DependencyEcosystem.Unsupported)
            {
                return (false, version);
            }

            if (!context.WebhookOptions.TrustedEntities.Publishers.TryGetValue(ecosystem, out var publishers) ||
                publishers.Count < 1)
            {
                return (false, version);
            }

            var registry = _registries.FirstOrDefault((p) => p.Ecosystem == ecosystem);

            if (registry is null)
            {
                return (false, version);
            }

            version ??= TryGetDependencyVersion(ecosystem, dependency, commitMessage, diff);

            if (string.IsNullOrWhiteSpace(version))
            {
                return (false, version);
            }

            if (await IsTrustedDependencyOwnerAsync(repository, dependency, version, publishers, registry))
            {
                return (true, version);
            }

            return (await IsTrustedDependencyVersionAsync(repository, ecosystem, dependency, version), version);
        }

        async Task<bool> IsTrustedDependencyOwnerAsync(
            RepositoryId repository,
            string dependency,
            string version,
            IList<string> publishers,
            IPackageRegistry registry)
        {
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
                        registry.Ecosystem);

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

        async Task<bool> IsTrustedDependencyVersionAsync(
            RepositoryId repository,
            DependencyEcosystem ecosystem,
            string dependency,
            string version)
        {
            try
            {
                if (await trustStore.IsTrustedAsync(ecosystem, dependency, version))
                {
                    Log.ImplicitlyTrustedDependencyUpdated(
                        logger,
                        reference,
                        repository,
                        dependency,
                        version,
                        ecosystem);

                    return true;
                }

                Log.DependencyNotImplicitlyTrusted(
                    logger,
                    reference,
                    repository,
                    dependency);
            }
            catch (Exception ex)
            {
                Log.FailedToQueryTrustStore(logger, dependency, version, ecosystem, ex);
            }

            return false;
        }
    }

    private async Task<IReadOnlyList<(string Name, string? UpdateType)>> GetIgnoredDependenciesAsync(
        RepositoryId repository,
        string? reference,
        DependencyEcosystem ecosystem)
    {
        IReadOnlyList<(string Name, string? UpdateType)> dependencies = [];

        try
        {
            var configuration = await context.InstallationClient.Repository.Content.GetRawContentByRef(
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

                dependencies = [.. config.Updates
                    .Where((p) => string.Equals(p.PackageEcosystem, normalized, StringComparison.OrdinalIgnoreCase))
                    .SelectMany((p) => p.Ignore)
                    .Select((p) => (p.DependencyName, p.UpdateTypes?.FirstOrDefault()))];
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
            SkipEnabledCheck = true,
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
            DependencyEcosystem registry);

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Warning,
            Message = "Failed to parse dependabot configuration for repository {Repository}.")]
        public static partial void FailedToParseDependabotConfiguration(
            ILogger logger,
            RepositoryId repository,
            Exception exception);

        [LoggerMessage(
            EventId = 10,
            Level = LogLevel.Information,
            Message = "Reference {Reference} for {Repository} updates dependency {Dependency} which is ignored by the dependabot configuration.")]
        public static partial void UpdateToDependencyIsIgnoredByDependabot(
            ILogger logger,
            string? reference,
            RepositoryId repository,
            string dependency);

        [LoggerMessage(
            EventId = 11,
            Level = LogLevel.Information,
            Message = "Reference {Reference} for {Repository} updates dependency {Dependency} version {Version} for the {Registry} registry which is implicitly trusted.")]
        public static partial void ImplicitlyTrustedDependencyUpdated(
            ILogger logger,
            string? reference,
            RepositoryId repository,
            string dependency,
            string version,
            DependencyEcosystem registry);

        [LoggerMessage(
            EventId = 12,
            Level = LogLevel.Debug,
            Message = "Reference {Reference} for {Repository} updates dependency {Dependency} which is not implicitly trusted.")]
        public static partial void DependencyNotImplicitlyTrusted(
            ILogger logger,
            string? reference,
            RepositoryId repository,
            string dependency);

        [LoggerMessage(
            EventId = 13,
            Level = LogLevel.Error,
            Message = "Failed to query trust store for package {PackageId} version {PackageVersion} from the {Ecosystem} ecosystem.")]
        public static partial void FailedToQueryTrustStore(
            ILogger logger,
            string packageId,
            string packageVersion,
            DependencyEcosystem ecosystem,
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

        [YamlMember(Alias = "update-types")]
        public IList<string>? UpdateTypes { get; set; }
    }

    private sealed class DependabotMetadata
    {
        [YamlMember(Alias = "updated-dependencies")]
        public IList<Dependency> Dependencies { get; set; } = [];
    }

    private sealed class Dependency
    {
        [YamlMember(Alias = "dependency-name")]
        public string DependencyName { get; set; } = string.Empty;

        [YamlMember(Alias = "dependency-version")]
        public string? DependencyVersion { get; set; }

        [YamlMember(Alias = "dependency-type")]
        public string DependencyType { get; set; } = string.Empty;

        [YamlMember(Alias = "update-type")]
        public string UpdateType { get; set; } = string.Empty;

        [YamlMember(Alias = "dependency-group")]
        public string? DependencyGroup { get; set; }
    }
}
