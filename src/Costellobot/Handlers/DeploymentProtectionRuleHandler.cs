// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events.DeploymentProtectionRule;
using Polly;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class DeploymentProtectionRuleHandler(
    IGitHubClientForInstallation client,
    PublicHolidayProvider publicHolidayProvider,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<DeploymentProtectionRuleHandler> logger) : IHandler
{
    private static readonly ResiliencePipeline Pipeline = CreateResiliencePipeline();
    private readonly IOptionsMonitor<WebhookOptions> _options = options;

    public async Task HandleAsync(WebhookEvent message)
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

        var options = _options.CurrentValue;

        if (!options.Deploy)
        {
            Log.DeploymentProtectionRuleApprovalIsDisabled(
                logger,
                repository,
                body.Environment,
                body.Deployment.Id);

            return;
        }

        if (publicHolidayProvider.IsPublicHoliday())
        {
            Log.TodayIsAPublicHoliday(
                logger,
                repository,
                body.Environment,
                body.Deployment.Id);

            return;
        }

        try
        {
            var review = new ReviewDeploymentProtectionRule(
                body.Environment,
                PendingDeploymentReviewState.Approved,
                options.DeployComment);

            await Pipeline.ExecuteAsync(
                static async (state, _) => await state.client.WorkflowRuns().ReviewCustomProtectionRuleAsync(state.DeploymentCallbackUrl, state.review),
                (client, body.DeploymentCallbackUrl, review),
                CancellationToken.None);

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
            .AddRetry(new() { Delay = TimeSpan.FromSeconds(1) })
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
           Message = "Ignoring deployment protection rule check for {Repository} for environment {EnvironmentName} for deployment {DeploymentId} as deployment approval is disabled.")]
        public static partial void DeploymentProtectionRuleApprovalIsDisabled(
            ILogger logger,
            RepositoryId repository,
            string? environmentName,
            long deploymentId);

        [LoggerMessage(
           EventId = 5,
           Level = LogLevel.Information,
           Message = "Ignoring deployment protection rule check for {Repository} for environment {EnvironmentName} for deployment {DeploymentId} as it is a public holiday.")]
        public static partial void TodayIsAPublicHoliday(
            ILogger logger,
            RepositoryId repository,
            string? environmentName,
            long deploymentId);
    }
}
