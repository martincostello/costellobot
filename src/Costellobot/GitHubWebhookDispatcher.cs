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

    public static Queue<GitHubEvent> Messages { get; } = new Queue<GitHubEvent>();

    public Task DispatchAsync(GitHubEvent message)
    {
        Log.ProcessingWebhook(_logger, message.HookId);

        // TODO Do some actual work with the message
        Messages.Enqueue(message);

        _ = _client.GetLastApiInfo();

        Log.ProcessedWebhook(_logger, message.HookId);

        return Task.CompletedTask;
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
