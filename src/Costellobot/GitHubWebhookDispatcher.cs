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
    private readonly IOptionsSnapshot<GitHubOptions> _gitHubOptions;
    private readonly IOptionsSnapshot<WebhookOptions> _webhookOptions;

    public GitHubWebhookDispatcher(
        IGitHubClientForInstallation client,
        IOptionsSnapshot<GitHubOptions> gitHubOptions,
        IOptionsSnapshot<WebhookOptions> webhookOptions,
        ILogger<GitHubWebhookDispatcher> logger)
    {
        _client = client;
        _gitHubOptions = gitHubOptions;
        _webhookOptions = webhookOptions;
        _logger = logger;
    }

    public async Task DispatchAsync(GitHubEvent message)
    {
        Log.ProcessingWebhook(_logger, message.HookId);

        if (!IsValidInstallation(message))
        {
            Log.IncorrectInstallationWebhookIgnored(_logger, message.HookId, message.HookInstallationTargetId);
            return;
        }

        if (_webhookOptions.Value.Comment &&
            message.Body is { } body &&
            message.Event == "pull_request" &&
            body.Action == "opened" &&
            body.Repository is { } repo &&
            body.PullRequest is { } pr &&
            pr.User.Login == "martincostello")
        {
            await _client.Issue.Comment.Create(
                repo.Owner.Login,
                repo.Name,
                pr.Number,
                $"Hey @{pr.User.Login} :wave: - this comment is a test.");
        }

        Log.ProcessedWebhook(_logger, message.HookId);
    }

    private bool IsValidInstallation(GitHubEvent message)
        => string.Equals(
               message.HookInstallationTargetId,
               _gitHubOptions.Value.InstallationId.ToString(CultureInfo.InvariantCulture),
               StringComparison.Ordinal);

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

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Warning,
           Message = "Ignored webhook with ID {HookId} as the installation ID {InstallationId} is incorrect.")]
        public static partial void IncorrectInstallationWebhookIgnored(ILogger logger, string hookId, string installationId);
    }
}
