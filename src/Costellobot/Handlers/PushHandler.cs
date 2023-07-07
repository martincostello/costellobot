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
            push.Repository is not { Fork: false } repo ||
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

        if (DotNetDependencyFileChanged(filesChanged))
        {
            Log.CreatedRepositoryDispatchForPush(_logger, repo.Owner.Login, repo.Name, push.Ref);
            await CreateDispatchAsync(repo.FullName);
        }
    }

    private static bool DotNetDependencyFileChanged(HashSet<string> filesChanged)
    {
        if (filesChanged.Contains("global.json"))
        {
            // .NET SDK updated
            return true;
        }

        /*
        if (filesChanged.Contains("Directory.Packages.props"))
        {
            // NuGet package(s) added/removed or version(s) updated
            return true;
        }

        if (filesChanged.Any((p) => p.EndsWith(".csproj", StringComparison.Ordinal)))
        {
            // NuGet dependencies possibly changed
            return true;
        }
        */

        return false;
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
