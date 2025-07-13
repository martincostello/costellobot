// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.DeploymentRules;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events.DeploymentProtectionRule;
using Polly;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class DeploymentProtectionRuleHandler(
    GitHubWebhookContext context,
    IEnumerable<IDeploymentRule> deploymentRules,
    ILogger<DeploymentProtectionRuleHandler> logger) : IHandler
{
    private static readonly ResiliencePipeline Pipeline = CreateResiliencePipeline();

    public async Task HandleAsync(WebhookEvent message, CancellationToken cancellationToken)
    {
        if (message is not DeploymentProtectionRuleRequestedEvent body ||
            body.Repository is not { } repo)
        {
            return;
        }

        var repository = RepositoryId.Create(repo);

        Log.DeploymentProtectionRuleRequested(
            logger,
            repository,
            body.Environment,
            body.Deployment.Id,
            body.DeploymentCallbackUrl);

        (var approved, var ruleName) = await DeploymentRule.EvaluateAsync(deploymentRules, message, cancellationToken);

        if (!approved)
        {
            Log.DeploymentNotApproved(
                logger,
                repository,
                body.Environment,
                body.Deployment.Id,
                ruleName);

            return;
        }

        try
        {
            var review = new ReviewDeploymentProtectionRule(
                body.Environment,
                PendingDeploymentReviewState.Approved,
                context.WebhookOptions.DeployComment);

            await Pipeline.ExecuteAsync(
                static async (state, token) => await state.InstallationClient.WorkflowRuns().ReviewCustomProtectionRuleAsync(state.DeploymentCallbackUrl, state.review, token),
                (context.InstallationClient, body.DeploymentCallbackUrl, review),
                cancellationToken);

            Log.ApprovedDeployment(
                logger,
                repository,
                body.Environment,
                body.Deployment.Id);
        }
        catch (Exception ex)
        {
            Log.FailedToApproveDeployment(
                logger,
                ex,
                repository,
                body.Environment,
                body.Deployment.Id);
        }
    }

    private static ResiliencePipeline CreateResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                UseJitter = true,
            })
            .Build();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Received deployment protection rule check for {Repository} for environment {EnvironmentName} for deployment {DeploymentId} with deployment callback {DeploymentCallbackUrl}.")]
        public static partial void DeploymentProtectionRuleRequested(
            ILogger logger,
            RepositoryId repository,
            string? environmentName,
            long deploymentId,
            string? deploymentCallbackUrl);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "Approved deployment protection rule check for {Repository} for environment {EnvironmentName} for deployment {DeploymentId}.")]
        public static partial void ApprovedDeployment(
            ILogger logger,
            RepositoryId repository,
            string? environmentName,
            long deploymentId);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Failed to approve deployment protection rule check for {Repository} for environment {EnvironmentName} for deployment {DeploymentId}.")]
        public static partial void FailedToApproveDeployment(
            ILogger logger,
            Exception exception,
            RepositoryId repository,
            string? environmentName,
            long deploymentId);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Information,
            Message = "Ignoring deployment protection rule check for {Repository} in environment {EnvironmentName} for deployment {DeploymentId} as deployment was not approved when evaluating rule {RuleName}.")]
        public static partial void DeploymentNotApproved(
            ILogger logger,
            RepositoryId repository,
            string? environmentName,
            long deploymentId,
            string? ruleName);
    }
}
