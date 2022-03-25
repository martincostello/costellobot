// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Octokit;
using Terrajobst.GitHubEvents;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class PullRequestHandler : IHandler
{
    private readonly IGitHubClient _client;
    private readonly IOptionsSnapshot<WebhookOptions> _options;
    private readonly ILogger _logger;

    public PullRequestHandler(
        IGitHubClientForInstallation client,
        IOptionsSnapshot<WebhookOptions> options,
        ILogger<PullRequestHandler> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task HandleAsync(GitHubEvent message)
    {
        if (!IsNewPullRequestFromTrustedUser(message))
        {
            Log.IgnoringPullRequestFromUntrustedUser(
                _logger,
                message.Body.Repository.Owner.Login,
                message.Body.Repository.Name,
                message.Body.PullRequest.Number,
                message.Body.PullRequest.User.Login);

            return;
        }

        if (await IsTrustedDependencyUpdateAsync(message))
        {
            if (_options.Value.Approve)
            {
                await _client.PullRequest.Review.Create(
                    message.Body.Repository.Owner.Login,
                    message.Body.Repository.Name,
                    message.Body.PullRequest.Number,
                    new()
                    {
                        Body = "Auto-approving dependency update.",
                        Event = PullRequestReviewEvent.Approve,
                    });
            }

            if (_options.Value.Automerge)
            {
                // TODO
            }
        }
    }

    private bool IsNewPullRequestFromTrustedUser(GitHubEvent message)
    {
        if (message.Body is { } body &&
            body.Action == "opened" &&
            body.Repository is { } repo &&
            body.PullRequest is { } pr &&
            !pr.Draft)
        {
            return _options.Value.TrustedEntities.Users.Contains(
                pr.User.Login,
                StringComparer.Ordinal);
        }

        return false;
    }

    private async Task<bool> IsTrustedDependencyUpdateAsync(GitHubEvent message)
    {
        var commit = await _client.Repository.Commit.Get(
            message.Body.Repository.Owner.Login,
            message.Body.Repository.Name,
            message.Body.PullRequest.Head.Sha);

        string[] commitLines = commit.Commit.Message
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

        var trustedDependencies = _options.Value.TrustedEntities.Dependencies;

        if (dependencies.Count < 1 || trustedDependencies.Count < 1)
        {
            return false;
        }

        Log.PullRequestUpdatesDependencies(
            _logger,
            message.Body.Repository.Owner.Login,
            message.Body.Repository.Name,
            message.Body.PullRequest.Number,
            dependencies.ToArray());

        foreach (string dependency in dependencies)
        {
            if (!trustedDependencies.Any((p) => Regex.IsMatch(dependency, p)))
            {
                Log.UntrustedDependencyUpdated(
                    _logger,
                    message.Body.Repository.Owner.Login,
                    message.Body.Repository.Name,
                    message.Body.PullRequest.Number,
                    dependency);

                return false;
            }
        }

        Log.TrustedDependenciesUpdated(
            _logger,
            message.Body.Repository.Owner.Login,
            message.Body.Repository.Name,
            message.Body.PullRequest.Number,
            dependencies.Count);

        return true;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Ignoring pull request {Owner}/{Repository}#{Number} from {Login} as it is not from a trusted user.")]
        public static partial void IgnoringPullRequestFromUntrustedUser(
            ILogger logger,
            string owner,
            string repository,
            int number,
            string login);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Information,
           Message = "Pull request {Owner}/{Repository}#{Number} updates the following dependencies: {Dependencies}.")]
        public static partial void PullRequestUpdatesDependencies(
            ILogger logger,
            string owner,
            string repository,
            int number,
            string[] dependencies);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Information,
           Message = "Pull request {Owner}/{Repository}#{Number} updates dependency {Dependency} which is not trusted.")]
        public static partial void UntrustedDependencyUpdated(
            ILogger logger,
            string owner,
            string repository,
            int number,
            string dependency);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Information,
           Message = "Pull request {Owner}/{Repository}#{Number} updates {Count} trusted dependencies.")]
        public static partial void TrustedDependenciesUpdated(
            ILogger logger,
            string owner,
            string repository,
            int number,
            int count);
    }
}
