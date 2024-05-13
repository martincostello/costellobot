// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class PushHandler(
    IGitHubClientForInstallation client,
    ILogger<PushHandler> logger) : IHandler
{
    private const string PrefixForBranchName = "refs/heads/";
    private const string DefaultBranch = $"{PrefixForBranchName}main";
    private const string DotNetNextBranch = $"{PrefixForBranchName}dotnet-vnext";

    public async Task HandleAsync(WebhookEvent message)
    {
        if (message is not PushEvent push ||
            push.Repository is not { Fork: false, Language: "C#" } repo ||
            push.Commits is not { } commits ||
            push.Created ||
            push.Deleted ||
            (!string.Equals(push.Ref, DefaultBranch, StringComparison.Ordinal) &&
             !string.Equals(push.Ref, DotNetNextBranch, StringComparison.Ordinal)))
        {
            return;
        }

        var filesChanged = new HashSet<string>();

        foreach (var commit in commits)
        {
            foreach (var file in commit.Added)
            {
                filesChanged.Add(file);
            }

            foreach (var file in commit.Modified)
            {
                filesChanged.Add(file);
            }
        }

        if (DependencyFileChanged(filesChanged))
        {
            Log.CreatedRepositoryDispatchForPush(logger, repo.Owner.Login, repo.Name, push.Ref);
            await CreateDispatchAsync(repo.FullName, push.Ref, push.After);
        }
    }

    private static bool DependencyFileChanged(HashSet<string> filesChanged)
    {
        return filesChanged.Any(IsDependencyFile);

        static bool IsDependencyFile(string path)
        {
            return Path.GetExtension(path) switch
            {
                ".csproj" => true,
                _ => Path.GetFileName(path) switch
                {
                    "Directory.Packages.props" => IsFileInRepositoryRoot(path),
                    "global.json" => IsFileInRepositoryRoot(path),
                    "package.json" => true,
                    "package-lock.json" => true,
                    _ => false,
                },
            };

            static bool IsFileInRepositoryRoot(string path)
                => Path.GetDirectoryName(path) is "";
        }
    }

    private async Task CreateDispatchAsync(string repository, string reference, string sha)
    {
        // See https://github.com/martincostello/github-automation/blob/main/.github/workflows/dotnet-dependencies-updated.yml
        var dispatch = new
        {
            event_type = "dotnet_dependencies_updated",
            client_payload = new
            {
                repository,
                @ref = reference,
                ref_name = reference[PrefixForBranchName.Length..],
                sha,
            },
        };

        await client.RepositoryDispatchAsync("martincostello", "github-automation", dispatch);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Creating repository dispatch for push to {Owner}/{Repository} for ref {Reference}.")]
        public static partial void CreatedRepositoryDispatchForPush(
            ILogger logger,
            string? owner,
            string? repository,
            string? reference);
    }
}
