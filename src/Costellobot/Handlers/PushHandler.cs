// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class PushHandler : IHandler
{
    private const string DefaultBranch = "refs/heads/main";
    private const string DispatchDestination = "martincostello/github-automation";

    private readonly IGitHubClient _client;
    private readonly ILogger _logger;

    public PushHandler(
        IGitHubClientForInstallation client,
        ILogger<PushHandler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task HandleAsync(WebhookEvent message)
    {
        if (message is not PushEvent push ||
            push.Repository is not { Fork: false, Language: "C#" } repo ||
            push.Commits is not { } commits ||
            push.Created ||
            push.Deleted ||
            !string.Equals(push.Ref, DefaultBranch, StringComparison.Ordinal))
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
            Log.CreatedRepositoryDispatchForPush(_logger, repo.Owner.Login, repo.Name, push.Ref);
            await CreateDispatchAsync(repo.FullName);
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

    private async Task CreateDispatchAsync(string repository)
    {
        // See https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28#create-a-repository-dispatch-event
        var dispatch = new
        {
            event_type = "dotnet_dependencies_updated",
            client_payload = new { repository },
        };

        var uri = new Uri($"repos/{DispatchDestination}/dispatches", UriKind.Relative);
        var status = await _client.Connection.Post(uri, dispatch, "application/vnd.github+json");

        if (status is not System.Net.HttpStatusCode.NoContent)
        {
            throw new ApiException($"Failed to create repository dispatch event for push to {repository}.", status);
        }
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
