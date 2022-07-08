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
    private readonly GitCommitAnalyzer _commitAnalyzer;
    private readonly IOptionsMonitor<GitHubOptions> _gitHubOptions;
    private readonly IOptionsMonitor<WebhookOptions> _webhookOptions;
    private readonly ILogger _logger;

    public DeploymentStatusHandler(
        IGitHubClientForInstallation client,
        GitCommitAnalyzer commitAnalyzer,
        IOptionsMonitor<GitHubOptions> gitHubOptions,
        IOptionsMonitor<WebhookOptions> webhookOptions,
        ILogger<DeploymentStatusHandler> logger)
    {
        _client = client;
        _commitAnalyzer = commitAnalyzer;
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

        string owner = repo.Owner.Login;
        string name = repo.Name;
        var options = _webhookOptions.CurrentValue;

        if (!options.DeployEnvironments.Contains(deploy.Environment))
        {
            Log.IgnoringDeploymentStatusAsEnvironmentNotEnabled(_logger, body.DeploymentStatus.Id, owner, name, deploy.Environment);
            return;
        }

        if (!options.Deploy)
        {
            Log.AutomatedDeploymentApprovalIsDisabled(_logger, body.DeploymentStatus.Id, owner, name);
            return;
        }

        var activeDeployment = await GetActiveDeploymentAsync(
            owner,
            name,
            deploy.Id,
            deploy.Environment);

        if (activeDeployment is null)
        {
            return;
        }

        // Diff the active and pending deployment and verify that only trusted dependency
        // changes from trusted users contribute to the changes pending to be deployed.
        var comparison = await _client.Repository.Commit.Compare(
            owner,
            name,
            activeDeployment.Sha,
            deploy.Sha);

        if (comparison.Commits.Count < 1)
        {
            Log.NoCommitsFoundForPendingDeployment(_logger, activeDeployment.Id, deploy.Id, owner, name);
            return;
        }

        foreach (var commit in comparison.Commits)
        {
            if (commit.Parents.Count > 1)
            {
                // Ignore merge commits
                continue;
            }

            if (!options.TrustedEntities.Users.Contains(commit.Author.Login))
            {
                Log.UntrustedCommitAuthorFound(_logger, deploy.Id, owner, name, commit.Sha, commit.Author.Login);
                return;
            }

            if (!_commitAnalyzer.IsTrustedDependencyUpdate(repo.Owner.Login, repo.Name, commit))
            {
                Log.IgnoringCommitThatIsNotATrustedCommit(_logger, deploy.Id, owner, name, commit.Sha);
                return;
            }
        }

        var workflowsClient = _client.WorkflowRuns();

        var pendingDeployments = await workflowsClient.GetPendingDeploymentsAsync(
            repo.Owner.Login,
            repo.Name,
            run.Id);

        if (pendingDeployments.Count != 1)
        {
            Log.NoPendingDeploymentsFound(_logger, run.Id, owner, name, pendingDeployments.Count);
            return;
        }

        var deployment = pendingDeployments[0];

        await ApproveDeploymentAsync(owner, name, run.Id, deployment);
    }

    private async Task ApproveDeploymentAsync(
        string owner,
        string name,
        long runId,
        PendingDeployment deployment)
    {
        var workflowsClient = _client.WorkflowRuns();

        if (!deployment.CurrentUserCanApprove)
        {
            // HACK Use OAuth token for user that has permissions to review instead of the app.
            // This can be removed in the future if GitHub Apps can be allowed review deployments.
            _client.Connection.Credentials = new Credentials(_gitHubOptions.CurrentValue.AccessToken);
            workflowsClient = _client.WorkflowRuns();
        }

        try
        {
            await workflowsClient.ReviewPendingDeploymentsAsync(
                owner,
                name,
                runId,
                new()
                {
                    EnvironmentIds = new[] { deployment.Environment.Id },
                    Comment = _webhookOptions.CurrentValue.DeployComment,
                    State = "approved",
                });
        }
        catch (Exception ex)
        {
            Log.FailedToApproveDeployment(_logger, ex, runId, owner, name);
        }
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

    private async Task<Octokit.Deployment?> GetActiveDeploymentAsync(
        string owner,
        string name,
        long deploymentId,
        string environment)
    {
        var parameters = new Dictionary<string, string>(1)
        {
            ["environment"] = environment,
        };

        var connection = new ApiConnection(_client.Connection);
        var deployments = await connection.Get<Octokit.Deployment[]>(
            new Uri($"repos/{owner}/{name}/deployments", UriKind.Relative),
            parameters);

        var previousDeployments = deployments
            .Where((p) => p.Id != deploymentId)
            .ToList();

        if (previousDeployments.Count < 1)
        {
            Log.NoPreviousDeploymentsFound(_logger, deploymentId, owner, name);
            return null;
        }

        var previousDeployment = previousDeployments[0];

        var previousDeploymentStatuses = await _client.Repository.Deployment.Status.GetAll(
            owner,
            name,
            previousDeployment.Id);

        if (previousDeploymentStatuses.Count < 1)
        {
            Log.NoDeploymentStatusesFound(_logger, previousDeployment.Id, owner, name);
            return null;
        }

        bool isPreviousDeploymentActive = IsDeploymentActive(previousDeploymentStatuses);

        if (isPreviousDeploymentActive)
        {
            Log.FoundActiveDeployment(_logger, previousDeployment.Id, owner, name);
            return previousDeployment;
        }

        foreach (var deployment in previousDeployments.Skip(1))
        {
            previousDeploymentStatuses = await _client.Repository.Deployment.Status.GetAll(
                owner,
                name,
                deployment.Id);

            if (IsDeploymentActive(previousDeploymentStatuses))
            {
                Log.FoundActiveDeployment(_logger, deployment.Id, owner, name);
                return deployment;
            }
        }

        Log.NoActiveDeploymentFound(_logger, deploymentId, owner, name);
        return null;

        static bool IsDeploymentActive(IReadOnlyList<Octokit.DeploymentStatus> statuses)
        {
            if (statuses.Count < 1)
            {
                return false;
            }

            return IsDeploymentStatusActive(statuses[0]);
        }

        static bool IsDeploymentStatusActive(Octokit.DeploymentStatus status)
        {
            return status.State.Value switch
            {
                DeploymentState.Success => true,
                _ => false,
            };
        }
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
            string owner,
            string repository,
            string? action);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Information,
           Message = "Ignoring deployment status ID {DeploymentStatusId} for {Owner}/{Repository} with state {State}.")]
        public static partial void IgnoringDeploymentStatusAsNotWaiting(
            ILogger logger,
            long deploymentStatusId,
            string owner,
            string repository,
            DeploymentStatusState state);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Information,
           Message = "Ignoring deployment status ID {DeploymentStatusId} for {Owner}/{Repository} as auto-deploy is not environment for the {Environment} environment.")]
        public static partial void IgnoringDeploymentStatusAsEnvironmentNotEnabled(
            ILogger logger,
            long deploymentStatusId,
            string owner,
            string repository,
            string environment);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Information,
           Message = "Ignoring deployment status ID {DeploymentStatusId} for {Owner}/{Repository} as deployment approval is disabled.")]
        public static partial void AutomatedDeploymentApprovalIsDisabled(
            ILogger logger,
            long deploymentStatusId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 5,
           Level = LogLevel.Information,
           Message = "No previous deployments found for deployment ID {DeploymentId} for {Owner}/{Repository}.")]
        public static partial void NoPreviousDeploymentsFound(
            ILogger logger,
            long deploymentId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 6,
           Level = LogLevel.Information,
           Message = "No previous deployment statuses found for deployment ID {DeploymentId} for {Owner}/{Repository}.")]
        public static partial void NoDeploymentStatusesFound(
            ILogger logger,
            long deploymentId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 7,
           Level = LogLevel.Information,
           Message = "Deployment ID {DeploymentId} for {Owner}/{Repository} is currently active.")]
        public static partial void FoundActiveDeployment(
            ILogger logger,
            long deploymentId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 8,
           Level = LogLevel.Information,
           Message = "No active deployment found for deployment ID {DeploymentId} for {Owner}/{Repository}.")]
        public static partial void NoActiveDeploymentFound(
            ILogger logger,
            long deploymentId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 9,
           Level = LogLevel.Information,
           Message = "No commits found between active deployment ID {ActiveDeploymentId} and pending deployment ID {PendingDeploymentId} for {Owner}/{Repository}.")]
        public static partial void NoCommitsFoundForPendingDeployment(
            ILogger logger,
            long activeDeploymentId,
            long pendingDeploymentId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 10,
           Level = LogLevel.Information,
           Message = "Deployment ID {DeploymentId} for {Owner}/{Repository} cannot be auto-deployed as it contains commit {Sha} from untrusted author {Login}.")]
        public static partial void UntrustedCommitAuthorFound(
            ILogger logger,
            long deploymentId,
            string owner,
            string repository,
            string sha,
            string login);

        [LoggerMessage(
           EventId = 11,
           Level = LogLevel.Information,
           Message = "Deployment ID {DeploymentId} for {Owner}/{Repository} cannot be auto-deployed as it contains commit {Sha} that is not a trusted dependency update.")]
        public static partial void IgnoringCommitThatIsNotATrustedCommit(
            ILogger logger,
            long deploymentId,
            string owner,
            string repository,
            string sha);

        [LoggerMessage(
           EventId = 12,
           Level = LogLevel.Information,
           Message = "Did not find exactly one pending deployments found for workflow run ID {RunId} for {Owner}/{Repository}; found {Count}.")]
        public static partial void NoPendingDeploymentsFound(
            ILogger logger,
            long runId,
            string owner,
            string repository,
            int count);

        [LoggerMessage(
           EventId = 13,
           Level = LogLevel.Warning,
           Message = "Failed to approve workflow run ID {RunId} for {Owner}/{Repository}.")]
        public static partial void FailedToApproveDeployment(
            ILogger logger,
            Exception exception,
            long runId,
            string owner,
            string repository);
    }
}
