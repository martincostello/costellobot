// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

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
        var clientFactory = Substitute.For<IGitHubClientFactory>();

        using var cache = new ApplicationCache();
        var trustStore = Substitute.For<ITrustStore>();

        var context = new GitHubWebhookContext(
            clientFactory,
            new GitHubOptions().ToMonitor(),
            options)
        {
            AppId = GitHubFixtures.AppId,
            InstallationId = GitHubFixtures.InstallationId,
        };

        var commitAnalyzer = new GitCommitAnalyzer(
            context,
            [],
            trustStore,
            NullLoggerFactory.Instance.CreateLogger<GitCommitAnalyzer>());

        var pullRequestAnalyzer = new PullRequestAnalyzer(
            context,
            commitAnalyzer,
            NullLoggerFactory.Instance.CreateLogger<PullRequestAnalyzer>());

        var pullRequestApprover = new PullRequestApprover(
            context,
            NullLoggerFactory.Instance.CreateLogger<PullRequestApprover>());

        var publicHolidayProvider = new PublicHolidayProvider(
            TimeProvider.System,
            NullLoggerFactory.Instance.CreateLogger<PublicHolidayProvider>());

        var serviceProvider = Substitute.For<IServiceProvider>();

        serviceProvider.GetService(typeof(CheckSuiteHandler))
            .Returns((_) =>
            {
                return new CheckSuiteHandler(
                    context,
                    NullLoggerFactory.Instance.CreateLogger<CheckSuiteHandler>());
            });

        serviceProvider.GetService(typeof(DeploymentProtectionRuleHandler))
            .Returns((_) =>
            {
                return new DeploymentProtectionRuleHandler(
                    context,
                    NullLoggerFactory.Instance.CreateLogger<DeploymentProtectionRuleHandler>());
            });

        serviceProvider.GetService(typeof(DeploymentStatusHandler))
            .Returns((_) =>
            {
                return new DeploymentStatusHandler(
                    context,
                    commitAnalyzer,
                    publicHolidayProvider,
                    NullLoggerFactory.Instance.CreateLogger<DeploymentStatusHandler>());
            });

        serviceProvider.GetService(typeof(IssueCommentHandler))
            .Returns((_) =>
            {
                return new IssueCommentHandler(
                    context,
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
                    context,
                    pullRequestAnalyzer,
                    pullRequestApprover,
                    cache,
                    trustStore,
                    NullLoggerFactory.Instance.CreateLogger<PullRequestReviewHandler>());
            });

        serviceProvider.GetService(typeof(PushHandler))
            .Returns((_) =>
            {
                return new PushHandler(
                    context,
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
