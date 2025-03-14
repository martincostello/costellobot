// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Octokit;

namespace MartinCostello.Costellobot.Handlers;

public static class HandlerFactoryTests
{
    [Theory]
    [InlineData(null, typeof(NullHandler))]
    [InlineData("", typeof(NullHandler))]
    [InlineData("check_run", typeof(NullHandler))]
    [InlineData("check_suite", typeof(CheckSuiteHandler))]
    [InlineData("deployment_protection_rule", typeof(DeploymentProtectionRuleHandler))]
    [InlineData("deployment_status", typeof(DeploymentStatusHandler))]
    [InlineData("installation", typeof(NullHandler))]
    [InlineData("installation_repositories", typeof(NullHandler))]
    [InlineData("issues", typeof(NullHandler))]
    [InlineData("issue_comment", typeof(IssueCommentHandler))]
    [InlineData("ping", typeof(NullHandler))]
    [InlineData("pull_request", typeof(PullRequestHandler))]
    [InlineData("pull_request_review", typeof(PullRequestReviewHandler))]
    [InlineData("push", typeof(PushHandler))]
    public static void Create_Creates_Correct_Handler_Type(string? eventType, Type expected)
    {
        // Arrange
        var options = new WebhookOptions().ToMonitor();

        var gitHubAppClient = Substitute.For<IGitHubClientForApp>();
        var gitHubInstallationClient = Substitute.For<IGitHubClientForInstallation>();
        var gitHubUserClient = Substitute.For<IGitHubClientForUser>();

        using var cache = new ApplicationCache();
        var trustStore = Substitute.For<ITrustStore>();

        var commitAnalyzer = new GitCommitAnalyzer(
            gitHubInstallationClient,
            [],
            trustStore,
            options,
            NullLoggerFactory.Instance.CreateLogger<GitCommitAnalyzer>());

        var pullRequestAnalyzer = new PullRequestAnalyzer(
            gitHubInstallationClient,
            commitAnalyzer,
            options,
            NullLoggerFactory.Instance.CreateLogger<PullRequestAnalyzer>());

        var pullRequestApprover = new PullRequestApprover(
            gitHubInstallationClient,
            Substitute.For<Octokit.GraphQL.IConnection>(),
            options,
            NullLoggerFactory.Instance.CreateLogger<PullRequestApprover>());

        var publicHolidayProvider = new PublicHolidayProvider(
            TimeProvider.System,
            NullLoggerFactory.Instance.CreateLogger<PublicHolidayProvider>());

        var serviceProvider = Substitute.For<IServiceProvider>();

        serviceProvider.GetService(typeof(CheckSuiteHandler))
            .Returns((_) =>
            {
                return new CheckSuiteHandler(
                    gitHubInstallationClient,
                    options,
                    NullLoggerFactory.Instance.CreateLogger<CheckSuiteHandler>());
            });

        serviceProvider.GetService(typeof(DeploymentProtectionRuleHandler))
            .Returns((_) =>
            {
                return new DeploymentProtectionRuleHandler(
                    gitHubInstallationClient,
                    publicHolidayProvider,
                    options,
                    NullLoggerFactory.Instance.CreateLogger<DeploymentProtectionRuleHandler>());
            });

        serviceProvider.GetService(typeof(DeploymentStatusHandler))
            .Returns((_) =>
            {
                return new DeploymentStatusHandler(
                    gitHubInstallationClient,
                    gitHubUserClient,
                    commitAnalyzer,
                    publicHolidayProvider,
                    options,
                    NullLoggerFactory.Instance.CreateLogger<DeploymentStatusHandler>());
            });

        serviceProvider.GetService(typeof(IssueCommentHandler))
            .Returns((_) =>
            {
                return new IssueCommentHandler(
                    gitHubInstallationClient,
                    NullLoggerFactory.Instance.CreateLogger<IssueCommentHandler>());
            });

        serviceProvider.GetService(typeof(PullRequestHandler))
            .Returns((_) =>
            {
                return new PullRequestHandler(
                    pullRequestAnalyzer,
                    pullRequestApprover,
                    options,
                    NullLoggerFactory.Instance.CreateLogger<PullRequestHandler>());
            });

        serviceProvider.GetService(typeof(PullRequestReviewHandler))
            .Returns((_) =>
            {
                return new PullRequestReviewHandler(
                    gitHubAppClient,
                    gitHubInstallationClient,
                    pullRequestAnalyzer,
                    pullRequestApprover,
                    cache,
                    trustStore,
                    options,
                    NullLoggerFactory.Instance.CreateLogger<PullRequestReviewHandler>());
            });

        serviceProvider.GetService(typeof(PushHandler))
            .Returns((_) =>
            {
                return new PushHandler(
                    gitHubInstallationClient,
                    NullLoggerFactory.Instance.CreateLogger<PushHandler>());
            });

        var target = new HandlerFactory(serviceProvider);

        // Act
        var actual = target.Create(eventType);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBeOfType(expected);
    }
}
