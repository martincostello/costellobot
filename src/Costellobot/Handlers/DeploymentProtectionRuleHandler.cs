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

    public async Task HandleAsync(WebhookEvent message)
    {
        if (message is not DeploymentProtectionRuleRequestedEvent body ||
            body.Repository is not { } repo)
        {
            return;
        }

        string owner = repo.Owner.Login;
        string name = repo.Name;

        Log.DeploymentProtectionRuleRequested(
            _logger,
            owner,
            name,
            body.Environment,
            body.Deployment.Id,
            body.DeploymentCallbackUrl);

        try
        {
            var review = new ReviewDeploymentProtectionRule(
                body.Environment,
                PendingDeploymentReviewState.Approved,
                _options.CurrentValue.DeployComment);

            await _client.WorkflowRuns().ReviewCustomProtectionRuleAsync(
                body.DeploymentCallbackUrl,
                review);
        }
        catch (Exception ex)
        {
            Log.FailedToApproveDeployment(
                _logger,
                ex,
                owner,
                name,
                body.Environment,
                body.Deployment.Id);
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Received deployment protection rule check for {Owner}/{Repository} for environment {EnvironmentName} for deployment {DeploymentId} with deployment callback {DeploymentCallbackUrl}.")]
        public static partial void DeploymentProtectionRuleRequested(
            ILogger logger,
            string? owner,
            string? repository,
            string? environmentName,
            long deploymentId,
            string? deploymentCallbackUrl);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Warning,
           Message = "Failed to approve deployment protection rule check for {Owner}/{Repository} for environment {EnvironmentName} for deployment {DeploymentId}.")]
        public static partial void FailedToApproveDeployment(
            ILogger logger,
            Exception exception,
            string? owner,
            string? repository,
            string? environmentName,
            long deploymentId);
    }
}
