// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Octokit;

namespace MartinCostello.Costellobot;

public sealed partial class GitCommitAnalyzer
{
    private readonly IOptionsMonitor<WebhookOptions> _options;
    private readonly ILogger _logger;

    public GitCommitAnalyzer(
        IOptionsMonitor<WebhookOptions> options,
        ILogger<GitCommitAnalyzer> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsTrustedDependencyUpdate(
        string owner,
        string name,
        GitHubCommit commit)
    {
        return IsTrustedDependencyUpdate(owner, name, commit.Sha, commit.Commit.Message);
    }

    public bool IsTrustedDependencyUpdate(
        string owner,
        string name,
        string sha,
        string commitMessage)
    {
        string[] commitLines = commitMessage
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var dependencies = new HashSet<string>();

        foreach (string line in commitLines)
        {
            const string Prefix = "- dependency-name: ";

            if (line.StartsWith(Prefix, StringComparison.Ordinal))
            {
                string dependencyName = line[Prefix.Length..];

                if (!string.IsNullOrEmpty(dependencyName))
                {
                    dependencies.Add(dependencyName.Trim());
                }
            }
        }

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
            // Sometimes the dependencies are wrapped with quotes,
            // for example "- dependency-name: "@actions/github"".
            string trimmed = dependency.Trim('"');

            if (!trustedDependencies.Any((p) => Regex.IsMatch(trimmed, p)))
            {
                Log.UntrustedDependencyUpdated(
                    _logger,
                    sha,
                    owner,
                    name,
                    trimmed);

                return false;
            }
        }

        Log.TrustedDependenciesUpdated(
            _logger,
            sha,
            owner,
            name,
            dependencies.Count);

        return true;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
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
           Message = "Commit {Sha} for {Owner}/{Repository} updates dependency {Dependency} which is not trusted.")]
        public static partial void UntrustedDependencyUpdated(
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
    }
}
