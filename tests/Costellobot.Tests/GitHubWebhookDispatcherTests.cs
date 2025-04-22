// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Handlers;
using NSubstitute;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public class GitHubWebhookDispatcherTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task Events_With_Incorrect_Installation_Id_Are_Ignored()
    {
        // Arrange
        var handlerFactory = Substitute.For<IHandlerFactory>();
        var target = CreateTarget(handlerFactory, installationId: 37);

        var message = Builders.GitHubFixtures.CreateEvent("pull_request", installationId: 99);

        // Act and Assert
        await Should.NotThrowAsync(() => target.DispatchAsync(message));
    }

    [Theory]
    [InlineData("installation")]
    [InlineData("installation_repositories")]
    [InlineData("pull_request")]
    public async Task Events_With_Correct_Installation_Id_Are_Processed(string eventName)
    {
        // Arrange
        var installationId = 42;
        var handler = Substitute.For<IHandler>();
        var handlerFactory = Substitute.For<IHandlerFactory>();

        handler.HandleAsync(Arg.Any<WebhookEvent>())
               .Returns(Task.CompletedTask);

        handlerFactory.Create(eventName)
                      .Returns(handler);

        var message = Builders.GitHubFixtures.CreateEvent(eventName, installationId: installationId);

        var target = CreateTarget(handlerFactory, installationId);

        // Act
        await target.DispatchAsync(message);

        // Assert
        handlerFactory.Received(1).Create(eventName);
        await handler.Received(1).HandleAsync(Arg.Is<WebhookEvent>((p) => p != null));
    }

    private GitHubWebhookDispatcher CreateTarget(
        IHandlerFactory handlerFactory,
        long installationId)
    {
        var app = new GitHubAppOptions()
        {
            AppId = GitHubFixtures.AppId,
            ClientId = "456",
            Name = "Costellobot",
            PrivateKey = string.Empty,
        };

        var installation = new GitHubInstallationOptions()
        {
            AppId = app.AppId,
        };

        var options = new GitHubOptions();

        options.Apps[app.AppId] = app;
        options.Installations[installationId.ToString(CultureInfo.InvariantCulture)] = installation;

        var clientFactory = Substitute.For<IGitHubClientFactory>();
        var logger = outputHelper.ToLogger<GitHubWebhookDispatcher>();
        var monitor = options.ToMonitor();

        var context = new GitHubWebhookContext(
            clientFactory,
            monitor,
            new WebhookOptions().ToMonitor())
        {
            AppId = GitHubFixtures.AppId,
            InstallationId = GitHubFixtures.InstallationId,
        };

        return new(
            context,
            handlerFactory,
            monitor,
            logger);
    }
}
