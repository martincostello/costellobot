﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.DeploymentRules;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.DeploymentStatus;
using Octokit.Webhooks.Models.DeploymentStatusEvent;
using Polly;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class DeploymentStatusHandler(
    GitHubWebhookContext context,
    GitCommitAnalyzer commitAnalyzer,
    IEnumerable<IDeploymentRule> deploymentRules,
    ILogger<DeploymentStatusHandler> logger) : IHandler
{
    private static readonly ResiliencePipeline Pipeline = CreateResiliencePipeline();

    public async Task HandleAsync(WebhookEvent message, CancellationToken cancellationToken)
    {
        if (message is not DeploymentStatusEvent body ||
            body.Deployment is not { } deploy ||
            body.DeploymentStatus is not { } status ||
            body.Repository is not { } repo ||
            body.WorkflowRun is not { } run)
        {
            return;
        }

        if (!IsDeploymentWaitingForApproval(body, out var repository))
        {
            return;
        }

        var options = context.WebhookOptions;

        if (!options.DeployEnvironments.Contains(deploy.Environment))
        {
            Log.IgnoringDeploymentStatusAsEnvironmentNotEnabled(logger, body.DeploymentStatus.Id, repository, deploy.Environment);
            return;
        }

        (var approved, var ruleName) = await DeploymentRule.EvaluateAsync(deploymentRules, message, cancellationToken);

        if (!approved)
        {
            Log.DeploymentNotApproved(logger, body.DeploymentStatus.Id, repository, ruleName);
            return;
        }

        var activeDeployment = await GetActiveDeploymentAsync(
            repository,
            deploy.Id,
            deploy.Environment);

        if (activeDeployment is null)
        {
            return;
        }

        if (!await CanDeployChangesAsync(repository, deploy, activeDeployment, cancellationToken))
        {
            return;
        }

        var workflowsClient = context.InstallationClient.WorkflowRuns();

        var pendingDeployments = await workflowsClient.GetPendingDeploymentsAsync(
            repo.Owner.Login,
            repo.Name,
            run.Id);

        if (pendingDeployments.Count > 0)
        {
            pendingDeployments = [.. pendingDeployments.Where((p) => p.Environment.Name == deploy.Environment)];
        }

        if (pendingDeployments.Count != 1)
        {
            Log.NoSinglePendingDeploymentFound(
                logger,
                run.Id,
                deploy.Environment,
                repository,
                pendingDeployments.Count);

            return;
        }

        var deployment = pendingDeployments[0];

        await ApproveDeploymentAsync(repository, run.Id, deployment, cancellationToken);
    }

    private static ResiliencePipeline CreateResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder().Handle<ApiValidationException>(),
                UseJitter = true,
            })
            .Build();
    }

    private async Task ApproveDeploymentAsync(
        RepositoryId repository,
        long runId,
        PendingDeployment deployment,
        CancellationToken cancellationToken)
    {
        // HACK Use OAuth token for user that has permissions to review instead of the app.
        // This can be removed in the future if GitHub Apps can be allowed review deployments.
        IGitHubClient client =
            deployment.CurrentUserCanApprove ?
            context.InstallationClient :
            context.UserClient;

        try
        {
            var review = new PendingDeploymentReview(
                [deployment.Environment.Id],
                PendingDeploymentReviewState.Approved,
                context.WebhookOptions.DeployComment);

            await Pipeline.ExecuteAsync(
                static async (state, _) => await state.client.Actions.Workflows.Runs.ReviewPendingDeployments(state.repository.Owner, state.repository.Name, state.runId, state.review),
                (client, repository, runId, review),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Log.FailedToApproveDeployment(logger, ex, runId, repository);
        }
    }

    private bool IsDeploymentWaitingForApproval(DeploymentStatusEvent message, out RepositoryId repository)
    {
        repository = RepositoryId.Create(message.Repository!);

        if (!string.Equals(message.Action, DeploymentStatusActionValue.Created, StringComparison.Ordinal))
        {
            Log.IgnoringDeploymentStatusAction(
                logger,
                message.DeploymentStatus.Id,
                repository,
                message.Action);

            return false;
        }

        if (message.DeploymentStatus.State.Value != DeploymentStatusState.Waiting)
        {
            Log.IgnoringDeploymentStatusAsNotWaiting(
                logger,
                message.DeploymentStatus.Id,
                repository,
                message.DeploymentStatus.State.Value);

            return false;
        }

        return true;
    }

    private async Task<Octokit.Deployment?> GetActiveDeploymentAsync(
        RepositoryId repository,
        long deploymentId,
        string environment)
    {
        var parameters = new Dictionary<string, string>(1)
        {
            ["environment"] = environment,
        };

        var connection = new ApiConnection(context.InstallationClient.Connection);
        var deployments = await connection.Get<Octokit.Deployment[]>(
            new Uri($"repos/{repository.FullName}/deployments", UriKind.Relative),
            parameters);

        // Because the list is sorted by time descending, we need to skip over any
        // deployments before the current deployment in the list to find the correct
        // previous deployment to compare against the deployment for this delivery.
        var previousDeployments = deployments
            .SkipWhile((p) => p.Id != deploymentId)
            .Where((p) => p.Id != deploymentId)
            .ToList();

        if (previousDeployments.Count < 1)
        {
            Log.NoPreviousDeploymentsFound(logger, deploymentId, repository);
            return null;
        }

        var previousDeployment = previousDeployments[0];

        var previousDeploymentStatuses = await context.InstallationClient.Repository.Deployment.Status.GetAll(
            repository.Owner,
            repository.Name,
            previousDeployment.Id);

        if (previousDeploymentStatuses.Count < 1)
        {
            Log.NoDeploymentStatusesFound(logger, previousDeployment.Id, repository);
            return null;
        }

        bool isPreviousDeploymentActive = IsDeploymentActive(previousDeploymentStatuses);

        if (isPreviousDeploymentActive)
        {
            Log.FoundActiveDeployment(logger, previousDeployment.Id, repository);
            return previousDeployment;
        }

        foreach (var deployment in previousDeployments.Skip(1))
        {
            previousDeploymentStatuses = await context.InstallationClient.Repository.Deployment.Status.GetAll(
                repository.Owner,
                repository.Name,
                deployment.Id);

            if (IsDeploymentActive(previousDeploymentStatuses))
            {
                Log.FoundActiveDeployment(logger, deployment.Id, repository);
                return deployment;
            }
        }

        Log.NoActiveDeploymentFound(logger, deploymentId, repository);
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

    private async Task<(string? Ref, string? Diff)> GetRefAndDiffForCommitFromPullRequestAsync(
        RepositoryId repository,
        string sha,
        CancellationToken cancellationToken)
    {
        // See https://docs.github.com/en/rest/commits/commits#list-pull-requests-associated-with-a-commit.
        var pullRequests = await context.InstallationClient.Connection.GetResponse<PullRequest[]>(
            new($"repos/{repository.FullName}/commits/{sha}/pulls", UriKind.Relative),
            cancellationToken);

        if (pullRequests.Body is { Length: 1 } pulls)
        {
            var pullRequest = pulls[0];
            string reference = pullRequest.Head.Ref;

#pragma warning disable CA1873
            if (logger.IsEnabled(LogLevel.Debug))
            {
                Log.FoundReferenceForCommitPullRequest(
                    logger,
                    sha,
                    reference,
                    new IssueId(repository, pullRequest.Number));
            }
#pragma warning restore CA1873

            var diff = await GetDiffAsync(pullRequest.Url, cancellationToken);

            return (reference, diff);
        }

        return default;
    }

    private async Task<string?> GetDiffAsync(string diffUrl, CancellationToken cancellationToken)
    {
        try
        {
            return await context.InstallationClient.GetDiffAsync(diffUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.GetDiffFailed(logger, ex, diffUrl);
            return null;
        }
    }

    private async Task<bool> CanDeployChangesAsync(
        RepositoryId repository,
        Octokit.Webhooks.Models.DeploymentStatusEvent.Deployment pendingDeployment,
        Octokit.Deployment activeDeployment,
        CancellationToken cancellationToken)
    {
        // Diff the active and pending deployment and verify that only trusted dependency
        // changes from trusted users contribute to the changes pending to be deployed.
        var comparison = await context.InstallationClient.Repository.Commit.Compare(
            repository.Owner,
            repository.Name,
            activeDeployment.Sha,
            pendingDeployment.Sha);

        if (comparison.Commits.Count < 1)
        {
            Log.NoCommitsFoundForPendingDeployment(logger, activeDeployment.Id, pendingDeployment.Id, repository);
            return false;
        }

        if (comparison.Status is not "ahead")
        {
            Log.PendingDeploymentIsBehindTheActiveDeployment(logger, pendingDeployment.Id, repository, comparison.BehindBy);
            return false;
        }

        foreach (var commit in comparison.Commits)
        {
            if (commit.Parents.Count > 1)
            {
                // Ignore merge commits
                continue;
            }

            if (!context.WebhookOptions.TrustedEntities.Users.Contains(commit.Author.Login))
            {
                Log.UntrustedCommitAuthorFound(logger, pendingDeployment.Id, repository, commit.Sha, commit.Author.Login);
                return false;
            }

            (string? reference, string? diff) = await GetRefAndDiffForCommitFromPullRequestAsync(
                repository,
                commit.Sha,
                cancellationToken);

            bool isTrustedCommit = await commitAnalyzer.IsTrustedDependencyUpdateAsync(
                repository,
                reference,
                commit,
                diff,
                cancellationToken);

            if (!isTrustedCommit)
            {
                Log.IgnoringCommitThatIsNotATrustedCommit(logger, pendingDeployment.Id, repository, commit.Sha);
                return false;
            }
        }

        return true;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Ignoring deployment status ID {DeploymentStatusId} for {Repository} for action {Action}.")]
        public static partial void IgnoringDeploymentStatusAction(
            ILogger logger,
            long deploymentStatusId,
            RepositoryId repository,
            string? action);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Ignoring deployment status ID {DeploymentStatusId} for {Repository} with state {State}.")]
        public static partial void IgnoringDeploymentStatusAsNotWaiting(
            ILogger logger,
            long deploymentStatusId,
            RepositoryId repository,
            DeploymentStatusState state);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Ignoring deployment status ID {DeploymentStatusId} for {Repository} as auto-deploy is not enabled for the {Environment} environment.")]
        public static partial void IgnoringDeploymentStatusAsEnvironmentNotEnabled(
            ILogger logger,
            long deploymentStatusId,
            RepositoryId repository,
            string environment);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Information,
            Message = "Ignoring deployment status ID {DeploymentStatusId} for {Repository} as deployment was not approved when evaluating rule {RuleName}.")]
        public static partial void DeploymentNotApproved(
            ILogger logger,
            long deploymentStatusId,
            RepositoryId repository,
            string? ruleName);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Information,
            Message = "No previous deployments found for deployment ID {DeploymentId} for {Repository}.")]
        public static partial void NoPreviousDeploymentsFound(
            ILogger logger,
            long deploymentId,
            RepositoryId repository);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Information,
            Message = "No previous deployment statuses found for deployment ID {DeploymentId} for {Repository}.")]
        public static partial void NoDeploymentStatusesFound(
            ILogger logger,
            long deploymentId,
            RepositoryId repository);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Information,
            Message = "Deployment ID {DeploymentId} for {Repository} is currently active.")]
        public static partial void FoundActiveDeployment(
            ILogger logger,
            long deploymentId,
            RepositoryId repository);

        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Information,
            Message = "No active deployment found for deployment ID {DeploymentId} for {Repository}.")]
        public static partial void NoActiveDeploymentFound(
            ILogger logger,
            long deploymentId,
            RepositoryId repository);

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Information,
            Message = "No commits found between active deployment ID {ActiveDeploymentId} and pending deployment ID {PendingDeploymentId} for {Repository}.")]
        public static partial void NoCommitsFoundForPendingDeployment(
            ILogger logger,
            long activeDeploymentId,
            long pendingDeploymentId,
            RepositoryId repository);

        [LoggerMessage(
            EventId = 10,
            Level = LogLevel.Debug,
            Message = "Deployment ID {DeploymentId} for {Repository} cannot be auto-deployed as it contains commit {Sha} from untrusted author {Login}.")]
        public static partial void UntrustedCommitAuthorFound(
            ILogger logger,
            long deploymentId,
            RepositoryId repository,
            string sha,
            string login);

        [LoggerMessage(
            EventId = 11,
            Level = LogLevel.Debug,
            Message = "Deployment ID {DeploymentId} for {Repository} cannot be auto-deployed as it contains commit {Sha} that is not a trusted dependency update.")]
        public static partial void IgnoringCommitThatIsNotATrustedCommit(
            ILogger logger,
            long deploymentId,
            RepositoryId repository,
            string sha);

        [LoggerMessage(
            EventId = 12,
            Level = LogLevel.Information,
            Message = "Did not find exactly one pending deployment for workflow run ID {RunId} for environment {EnvironmentName} for {Repository}; found {Count}.")]
        public static partial void NoSinglePendingDeploymentFound(
            ILogger logger,
            long runId,
            string environmentName,
            RepositoryId repository,
            int count);

        [LoggerMessage(
            EventId = 13,
            Level = LogLevel.Warning,
            Message = "Failed to approve workflow run ID {RunId} for {Repository}.")]
        public static partial void FailedToApproveDeployment(
            ILogger logger,
            Exception exception,
            long runId,
            RepositoryId repository);

        [LoggerMessage(
            EventId = 14,
            Level = LogLevel.Debug,
            SkipEnabledCheck = true,
            Message = "Commit {Sha} is associated with reference {Reference} from pull request {PullRequest}.")]
        public static partial void FoundReferenceForCommitPullRequest(
            ILogger logger,
            string sha,
            string reference,
            IssueId pullRequest);

        [LoggerMessage(
            EventId = 15,
            Level = LogLevel.Debug,
            Message = "The pending deployment ID {PendingDeploymentId} for {Repository} is {Count} commits behind.")]
        public static partial void PendingDeploymentIsBehindTheActiveDeployment(
            ILogger logger,
            long pendingDeploymentId,
            RepositoryId repository,
            int count);

        [LoggerMessage(
            EventId = 16,
            Level = LogLevel.Warning,
            Message = "Failed to get Git diff from URL {GitDiffUrl}.")]
        public static partial void GetDiffFailed(
            ILogger logger,
            Exception exception,
            string gitDiffUrl);
    }
}
