// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using MartinCostello.Costellobot.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public class GitHubWebhookServiceTests
{
    public GitHubWebhookServiceTests(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
    }

    private ITestOutputHelper OutputHelper { get; }

    [Fact]
    public async Task ProcessAsync_Handles_Exception()
    {
        // Arrange
        long installationId = 23;

        var queue = new GitHubWebhookQueue(OutputHelper.ToLogger<GitHubWebhookQueue>());
        var serviceProvider = CreateServiceProvider(installationId);

        var target = new GitHubWebhookService(
            queue,
            serviceProvider,
            OutputHelper.ToLogger<GitHubWebhookService>());

        var message = new GitHubEvent(
            new(),
            new Octokit.Webhooks.Events.PullRequest.PullRequestOpenedEvent()
            {
                Installation = new()
                {
                    Id = installationId,
                },
            },
            new Dictionary<string, string>(),
            JsonDocument.Parse("{}").RootElement.Clone());

        // Act
        await target.ProcessAsync(message);

        // Assert
        var handler = serviceProvider.GetRequiredService<IHandler>();
        var mock = Mock.Get(handler);

        mock.Verify(
            (p) => p.HandleAsync(It.IsAny<WebhookEvent>()),
            Times.Once());
    }

    private IServiceProvider CreateServiceProvider(long installationId)
    {
        var options = new GitHubOptions()
        {
            InstallationId = installationId,
        }.ToMonitor();

        var handler = new Mock<IHandler>();

        handler
            .Setup((p) => p.HandleAsync(It.IsAny<WebhookEvent>()))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .Verifiable();

        var handlerFactory = new Mock<IHandlerFactory>();

        handlerFactory
            .Setup((p) => p.Create(It.IsAny<string?>()))
            .Returns(handler.Object);

        var dispatcher = new GitHubWebhookDispatcher(
            handlerFactory.Object,
            options,
            OutputHelper.ToLogger<GitHubWebhookDispatcher>());

        var serviceProvider = new Mock<IServiceProvider>();
        var serviceScope = new Mock<IServiceScope>();
        var serviceScopeFactory = new Mock<IServiceScopeFactory>();

        serviceScope
            .Setup((p) => p.ServiceProvider)
            .Returns(() => serviceProvider.Object);

        serviceScopeFactory
            .Setup((p) => p.CreateScope())
            .Returns(() => serviceScope.Object);

        serviceProvider
            .Setup((p) => p.GetService(typeof(IHandler)))
            .Returns(handler.Object);

        serviceProvider
            .Setup((p) => p.GetService(typeof(IServiceScope)))
            .Returns(serviceScope.Object);

        serviceProvider
            .Setup((p) => p.GetService(typeof(IServiceScopeFactory)))
            .Returns(serviceScopeFactory.Object);

        serviceProvider
            .Setup((p) => p.GetService(typeof(GitHubWebhookDispatcher)))
            .Returns(dispatcher);

        return serviceProvider.Object;
    }
}
