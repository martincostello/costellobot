// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Handlers;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubWebhookDispatcher(
    IHandlerFactory handlerFactory,
    IOptionsMonitor<GitHubOptions> options,
    ILogger<GitHubWebhookDispatcher> logger)
{
    public async Task DispatchAsync(GitHubEvent message)
    {
        Log.ProcessingWebhook(logger, message.Headers.Delivery, message);

        if (!IsValidInstallation(message))
        {
            Log.IncorrectInstallationWebhookIgnored(logger, message.Headers.Delivery, message, message.Event.Installation?.Id);
            return;
        }

        try
        {
            var handler = handlerFactory.Create(message.Headers.Event);
            await handler.HandleAsync(message.Event);

            Log.ProcessedWebhook(logger, message.Headers.Delivery, message);
        }
        catch (Exception ex)
        {
            Log.WebhookProcessingFailed(logger, ex, message.Headers.Delivery, message);
            throw;
        }
    }

    private bool IsValidInstallation(GitHubEvent message)
    {
        long? installationId = message.Event switch
        {
            Octokit.Webhooks.Events.InstallationEvent installation => installation.Installation.Id,
            Octokit.Webhooks.Events.InstallationRepositoriesEvent repos => repos.Installation.Id,
            _ => message.Event.Installation?.Id,
        };

        return installationId == options.CurrentValue.InstallationId;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "Processing webhook with ID {HookId} for event {Event}.")]
        public static partial void ProcessingWebhook(ILogger logger, string? hookId, GitHubEvent @event);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Information,
           Message = "Processed webhook with ID {HookId} for event {Event}.")]
        public static partial void ProcessedWebhook(ILogger logger, string? hookId, GitHubEvent @event);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Warning,
           Message = "Ignored webhook with ID {HookId} for event {Event} as the installation ID {InstallationId} is incorrect.")]
        public static partial void IncorrectInstallationWebhookIgnored(ILogger logger, string? hookId, GitHubEvent @event, long? installationId);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Error,
           Message = "Failed to process webhook with ID {HookId} for event {Event}.")]
        public static partial void WebhookProcessingFailed(ILogger logger, Exception exception, string? hookId, GitHubEvent @event);
    }
}
