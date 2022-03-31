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
    [InlineData("deployment_status", typeof(DeploymentStatusHandler))]
    [InlineData("issues", typeof(NullHandler))]
    [InlineData("ping", typeof(NullHandler))]
    [InlineData("pull_request", typeof(PullRequestHandler))]
    public static void Create_Creates_Correct_Handler_Type(string? eventType, Type expected)
    {
        // Arrange
        var mock = new Mock<IServiceProvider>();

        mock.Setup((p) => p.GetService(typeof(CheckSuiteHandler)))
            .Returns(() =>
            {
                return new CheckSuiteHandler(
                    Mock.Of<IGitHubClientForInstallation>(),
                    new WebhookOptions().ToMonitor(),
                    NullLoggerFactory.Instance.CreateLogger<CheckSuiteHandler>());
            });

        mock.Setup((p) => p.GetService(typeof(DeploymentStatusHandler)))
            .Returns(() =>
            {
                return new DeploymentStatusHandler(
                    Mock.Of<IGitHubClientForInstallation>(),
                    new GitHubOptions().ToMonitor(),
                    new WebhookOptions().ToMonitor(),
                    NullLoggerFactory.Instance.CreateLogger<DeploymentStatusHandler>());
            });

        mock.Setup((p) => p.GetService(typeof(PullRequestHandler)))
            .Returns(() =>
            {
                return new PullRequestHandler(
                    Mock.Of<IGitHubClientForInstallation>(),
                    Mock.Of<Octokit.GraphQL.IConnection>(),
                    new WebhookOptions().ToMonitor(),
                    NullLoggerFactory.Instance.CreateLogger<PullRequestHandler>());
            });

        var target = new HandlerFactory(mock.Object);

        // Act
        var actual = target.Create(eventType);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBeOfType(expected);
    }
}
