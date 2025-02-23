﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

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
        var gitHubOptions = new GitHubOptions().ToMonitor();
        var webhookOptions = new WebhookOptions().ToMonitor();

        var gitHubInstallationClient = Substitute.For<IGitHubClientForInstallation>();

        var trustStore = Substitute.For<ITrustStore>();

        var commitAnalyzer = new GitCommitAnalyzer(
            gitHubInstallationClient,
            [],
            trustStore,
            webhookOptions,
            NullLoggerFactory.Instance.CreateLogger<GitCommitAnalyzer>());

        var pullRequestAnalyzer = new PullRequestAnalyzer(
            gitHubInstallationClient,
            commitAnalyzer,
            webhookOptions,
            NullLoggerFactory.Instance.CreateLogger<PullRequestAnalyzer>());

        var pullRequestApprover = new PullRequestApprover(
            gitHubInstallationClient,
            Substitute.For<Octokit.GraphQL.IConnection>(),
            webhookOptions,
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
                    webhookOptions,
                    NullLoggerFactory.Instance.CreateLogger<CheckSuiteHandler>());
            });

        serviceProvider.GetService(typeof(DeploymentProtectionRuleHandler))
            .Returns((_) =>
            {
                return new DeploymentProtectionRuleHandler(
                    gitHubInstallationClient,
                    publicHolidayProvider,
                    webhookOptions,
                    NullLoggerFactory.Instance.CreateLogger<DeploymentProtectionRuleHandler>());
            });

        serviceProvider.GetService(typeof(DeploymentStatusHandler))
            .Returns((_) =>
            {
                return new DeploymentStatusHandler(
                    gitHubInstallationClient,
                    commitAnalyzer,
                    publicHolidayProvider,
                    gitHubOptions,
                    webhookOptions,
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
                    webhookOptions,
                    NullLoggerFactory.Instance.CreateLogger<PullRequestHandler>());
            });

        serviceProvider.GetService(typeof(PullRequestReviewHandler))
            .Returns((_) =>
            {
                return new PullRequestReviewHandler();
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
