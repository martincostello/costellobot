// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Moq;

namespace MartinCostello.Costellobot;

public class GitHubWebhookDispatcherTests
{
    public GitHubWebhookDispatcherTests(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
    }

    private ITestOutputHelper OutputHelper { get; }

    [Fact]
    public async void Events_With_Incorrect_Installation_Id_Are_Ignored()
    {
        // Arrange
        var client = Mock.Of<IGitHubClientForInstallation>();
        var gitHubOptions = new GitHubOptions() { InstallationId = 37 }.ToSnapshot();
        var webhookOptions = new WebhookOptions().ToSnapshot();
        var logger = OutputHelper.ToLogger<GitHubWebhookDispatcher>();

        var message = Builders.GitHubFixtures.CreateEvent("pull_request", installationId: "99");

        var target = new GitHubWebhookDispatcher(
            client,
            gitHubOptions,
            webhookOptions,
            logger);

        // Act (no Assert)
        await target.DispatchAsync(message);
    }
}
