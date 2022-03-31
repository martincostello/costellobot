// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.DeploymentStatus;
using Octokit.Webhooks.Models.DeploymentStatusEvent;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class DeploymentStatusHandler : IHandler
{
    private readonly IGitHubClient _client;
    private readonly IOptionsMonitor<GitHubOptions> _gitHubOptions;
    private readonly IOptionsMonitor<WebhookOptions> _webhookOptions;
    private readonly ILogger _logger;

    public DeploymentStatusHandler(
        IGitHubClientForInstallation client,
        IOptionsMonitor<GitHubOptions> gitHubOptions,
        IOptionsMonitor<WebhookOptions> webhookOptions,
        ILogger<DeploymentStatusHandler> logger)
    {
        _client = client;
        _gitHubOptions = gitHubOptions;
        _webhookOptions = webhookOptions;
        _logger = logger;
    }

    public async Task HandleAsync(WebhookEvent message)
    {
        if (message is not DeploymentStatusEvent body ||
            body.Deployment is not { } deploy ||
            body.DeploymentStatus is not { } status ||
            body.Repository is not { } repo ||
            body.WorkflowRun is not { } run)
        {
            return;
        }

        if (!IsDeploymentWaitingForApproval(body))
        {
            return;
        }

        if (!_webhookOptions.CurrentValue.Deploy)
        {
            Log.IgnoringDeploymentStatusAsApprovalDisabled(
                _logger,
                body.DeploymentStatus.Id,
                body.Repository.Owner.Login,
                body.Repository.Name);
            return;
        }

        _client.Connection.Credentials = new(_gitHubOptions.CurrentValue.AccessToken);
        _ = _client.WorkflowRuns();

        // TODO Implement auto-deployment
        await Task.CompletedTask;
    }

    private bool IsDeploymentWaitingForApproval(DeploymentStatusEvent message)
    {
        if (!string.Equals(message.Action, DeploymentStatusAction.Created, StringComparison.Ordinal))
        {
            Log.IgnoringDeploymentStatusAction(
                _logger,
                message.DeploymentStatus.Id,
                message.Repository!.Owner.Login,
                message.Repository.Name,
                message.Action);

            return false;
        }

        if (message.DeploymentStatus.State != DeploymentStatusState.Waiting)
        {
            Log.IgnoringDeploymentStatusAsNotWaiting(
                _logger,
                message.DeploymentStatus.Id,
                message.Repository!.Owner.Login,
                message.Repository.Name,
                message.DeploymentStatus.State);

            return false;
        }

        return true;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Ignoring deployment status ID {DeploymentStatusId} for {Owner}/{Repository} for action {Action}.")]
        public static partial void IgnoringDeploymentStatusAction(
            ILogger logger,
            long deploymentStatusId,
            string? owner,
            string? repository,
            string? action);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Information,
           Message = "Ignoring deployment status ID {DeploymentStatusId} for {Owner}/{Repository} with state {State}.")]
        public static partial void IgnoringDeploymentStatusAsNotWaiting(
            ILogger logger,
            long deploymentStatusId,
            string? owner,
            string? repository,
            DeploymentStatusState state);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Information,
           Message = "Ignoring deployment status ID {DeploymentStatusId} for {Owner}/{Repository} as deployment approval is disabled.")]
        public static partial void IgnoringDeploymentStatusAsApprovalDisabled(
            ILogger logger,
            long deploymentStatusId,
            string? owner,
            string? repository);
    }
}
