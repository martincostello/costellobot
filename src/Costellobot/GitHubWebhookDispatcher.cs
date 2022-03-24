// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Octokit;
using Terrajobst.GitHubEvents;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubWebhookDispatcher
{
    private readonly IGitHubClient _client;
    private readonly ILogger _logger;
    private readonly IOptionsSnapshot<WebhookOptions> _options;

    public GitHubWebhookDispatcher(
        IGitHubClientForInstallation client,
        IOptionsSnapshot<WebhookOptions> options,
        ILogger<GitHubWebhookDispatcher> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task DispatchAsync(GitHubEvent message)
    {
        Log.ProcessingWebhook(_logger, message.HookId);

        if (_options.Value.Comment &&
            message.Body is { } body &&
            message.Event == "pull_request" &&
            body.Action == "opened" &&
            body.Repository is { } repo &&
            body.PullRequest is { } pr &&
            pr.User.Login == "martincostello")
        {
            var allEmoji = await _client.Miscellaneous.GetAllEmojis();

#pragma warning disable CA5394
            var emoji = allEmoji[Random.Shared.Next(0, allEmoji.Count)];
#pragma warning restore CA5394

            await _client.Issue.Comment.Create(
                repo.Owner.Login,
                repo.Name,
                pr.Number,
                $"Hey @{pr.User.Login} :wave: - this comment is a test. Enjoy this randomly chosen emoij: :{emoji.Name}:");
        }

        Log.ProcessedWebhook(_logger, message.HookId);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Processing webhook with ID {HookId}.")]
        public static partial void ProcessingWebhook(ILogger logger, string hookId);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Information,
           Message = "Processed webhook with ID {HookId}.")]
        public static partial void ProcessedWebhook(ILogger logger, string hookId);
    }
}
