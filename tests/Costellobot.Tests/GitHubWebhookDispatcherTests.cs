// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Handlers;
using NSubstitute;

namespace MartinCostello.Costellobot;

public class GitHubWebhookDispatcherTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task Events_With_Incorrect_Installation_Id_Are_Ignored()
    {
        // Arrange
        var handlerFactory = Substitute.For<IHandlerFactory>();
        var options = new GitHubOptions() { InstallationId = 37 }.ToMonitor();
        var logger = outputHelper.ToLogger<GitHubWebhookDispatcher>();

        var message = Builders.GitHubFixtures.CreateEvent("pull_request", installationId: 99);

        var target = new GitHubWebhookDispatcher(
            handlerFactory,
            options,
            logger);

        // Act
        await Should.NotThrowAsync(() => target.DispatchAsync(message));
    }
}
