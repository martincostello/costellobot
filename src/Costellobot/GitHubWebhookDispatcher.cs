// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Handlers;
using Microsoft.Extensions.Options;
using Terrajobst.GitHubEvents;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubWebhookDispatcher
{
    private readonly IHandlerFactory _handlerFactory;
    private readonly ILogger _logger;
    private readonly IOptionsSnapshot<GitHubOptions> _options;

    public GitHubWebhookDispatcher(
        IHandlerFactory handlerFactory,
        IOptionsSnapshot<GitHubOptions> options,
        ILogger<GitHubWebhookDispatcher> logger)
    {
        _handlerFactory = handlerFactory;
        _options = options;
        _logger = logger;
    }

    public async Task DispatchAsync(GitHubEvent message)
    {
        Log.ProcessingWebhook(_logger, message.HookId);

        if (!IsValidInstallation(message))
        {
            Log.IncorrectInstallationWebhookIgnored(_logger, message.HookId, message.Body.Installation.Id);
            return;
        }

        var handler = _handlerFactory.Create(message.Event);
        await handler.HandleAsync(message);

        Log.ProcessedWebhook(_logger, message.HookId);
    }

    private bool IsValidInstallation(GitHubEvent message)
        => message.Body.Installation.Id == _options.Value.InstallationId;

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
        public static partial void IncorrectInstallationWebhookIgnored(ILogger logger, string hookId, int installationId);
    }
}
