// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    [InlineData("issues", typeof(NullHandler))]
    [InlineData("ping", typeof(NullHandler))]
    [InlineData("pull_request", typeof(PullRequestHandler))]
    [InlineData("push", typeof(PushHandler))]
    public static void Create_Creates_Correct_Handler_Type(string? eventType, Type expected)
    {
        // Arrange
        var gitHubOptions = new GitHubOptions().ToMonitor();
        var webhookOptions = new WebhookOptions().ToMonitor();

        var gitHubClient = Mock.Of<IGitHubClientForInstallation>();

        var commitAnalyzer = new GitCommitAnalyzer(
            Array.Empty<Registries.IPackageRegistry>(),
            webhookOptions,
            NullLoggerFactory.Instance.CreateLogger<GitCommitAnalyzer>());

        var mock = new Mock<IServiceProvider>();

        mock.Setup((p) => p.GetService(typeof(CheckSuiteHandler)))
            .Returns(() =>
            {
                return new CheckSuiteHandler(
                    gitHubClient,
                    webhookOptions,
                    NullLoggerFactory.Instance.CreateLogger<CheckSuiteHandler>());
            });

        mock.Setup((p) => p.GetService(typeof(DeploymentProtectionRuleHandler)))
            .Returns(() =>
            {
                return new DeploymentProtectionRuleHandler(
                    gitHubClient,
                    webhookOptions,
                    NullLoggerFactory.Instance.CreateLogger<DeploymentProtectionRuleHandler>());
            });

        mock.Setup((p) => p.GetService(typeof(DeploymentStatusHandler)))
            .Returns(() =>
            {
                return new DeploymentStatusHandler(
                    gitHubClient,
                    commitAnalyzer,
                    gitHubOptions,
                    webhookOptions,
                    NullLoggerFactory.Instance.CreateLogger<DeploymentStatusHandler>());
            });

        mock.Setup((p) => p.GetService(typeof(PullRequestHandler)))
            .Returns(() =>
            {
                return new PullRequestHandler(
                    gitHubClient,
                    Mock.Of<Octokit.GraphQL.IConnection>(),
                    commitAnalyzer,
                    webhookOptions,
                    NullLoggerFactory.Instance.CreateLogger<PullRequestHandler>());
            });

        mock.Setup((p) => p.GetService(typeof(PushHandler)))
            .Returns(() =>
            {
                return new PushHandler(
                    gitHubClient,
                    NullLoggerFactory.Instance.CreateLogger<PushHandler>());
            });

        var target = new HandlerFactory(mock.Object);

        // Act
        var actual = target.Create(eventType);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBeOfType(expected);
    }
}
