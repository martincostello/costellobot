// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using MartinCostello.Costellobot.Handlers;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public class InMemoryGitHubJobTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task ProcessAsync_Handles_Exception()
    {
        // Arrange
        long installationId = 23;

        var queue = new GitHubWebhookQueue(outputHelper.ToLogger<GitHubWebhookQueue>());
        var serviceProvider = CreateServiceProvider(installationId);

        var target = new InMemoryGitHubJob(
            queue,
            serviceProvider,
            outputHelper.ToLogger<InMemoryGitHubJob>());

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

        await handler.Received(1).HandleAsync(Arg.Any<WebhookEvent>());
    }

    private IServiceProvider CreateServiceProvider(long installationId)
    {
        var options = new GitHubOptions()
        {
            InstallationId = installationId,
        }.ToMonitor();

        var handler = Substitute.For<IHandler>();

        handler.When((p) => p.HandleAsync(Arg.Any<WebhookEvent>()))
               .Throw(new InvalidOperationException("boom"));

        var handlerFactory = Substitute.For<IHandlerFactory>();

        handlerFactory
            .Create(Arg.Any<string?>())
            .Returns(handler);

        var dispatcher = new GitHubWebhookDispatcher(
            handlerFactory,
            options,
            outputHelper.ToLogger<GitHubWebhookDispatcher>());

        var serviceProvider = Substitute.For<IServiceProvider>();
        var serviceScope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();

        serviceScope
            .ServiceProvider
            .Returns(serviceProvider);

        serviceScopeFactory
            .CreateScope()
            .Returns(serviceScope);

        serviceProvider
            .GetService(typeof(IHandler))
            .Returns(handler);

        serviceProvider
            .GetService(typeof(IServiceScope))
            .Returns(serviceScope);

        serviceProvider
            .GetService(typeof(IServiceScopeFactory))
            .Returns(serviceScopeFactory);

        serviceProvider
            .GetService(typeof(GitHubWebhookDispatcher))
            .Returns(dispatcher);

        return serviceProvider;
    }
}
