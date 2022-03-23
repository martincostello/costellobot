// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Terrajobst.GitHubEvents;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubEventProcessor : IGitHubEventProcessor
{
    private readonly ILogger<GitHubEventProcessor> _logger;

    public GitHubEventProcessor(ILogger<GitHubEventProcessor> logger)
    {
        _logger = logger;
    }

    public void Process(GitHubEvent message)
    {
        Log.ReceivedWebHook(_logger, message.HookId);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Received webhook with ID {HookId}.")]
        public static partial void ReceivedWebHook(ILogger logger, string hookId);
    }
}
