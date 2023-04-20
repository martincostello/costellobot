// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events.DeploymentProtectionRule;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class DeploymentProtectionRuleHandler : IHandler
{
    private readonly IGitHubClient _client;
    private readonly IOptionsMonitor<WebhookOptions> _options;
    private readonly ILogger _logger;

    public DeploymentProtectionRuleHandler(
        IGitHubClientForInstallation client,
        IOptionsMonitor<WebhookOptions> options,
        ILogger<DeploymentProtectionRuleHandler> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public Task HandleAsync(WebhookEvent message)
    {
        if (message is DeploymentProtectionRuleRequestedEvent body &&
            body.Repository is { } repo)
        {
            string owner = repo.Owner.Login;
            string name = repo.Name;

            Log.DeploymentProtectionRuleRequested(
                _logger,
                owner,
                name,
                body.Environment,
                body.Event,
                body.Deployment.Id,
                body.DeploymentCallbackUrl);

            if (_client is not null && _options is not null)
            {
                // TODO Use these to approve, reject or comment on the request
            }
        }

        return Task.CompletedTask;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Received deployment protection rule check for {Owner}/{Repository} for environment {EnvironmentName} for event {EventName} and deployment {DeploymentId} with deployment callback {DeploymentCallbackUrl}.")]
        public static partial void DeploymentProtectionRuleRequested(
            ILogger logger,
            string? owner,
            string? repository,
            string? environmentName,
            string? eventName,
            long deploymentId,
            string? deploymentCallbackUrl);
    }
}
