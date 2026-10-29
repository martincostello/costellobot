// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Drivers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace MartinCostello.Costellobot;

public static class ILoggerExtensionsTests
{
    [Fact]
    public static void BeginWebhookScope_Does_Not_Create_Scope_If_Logging_Is_Disabled()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(LogLevel.Critical).Returns(false);

        var message = CreateMessage();

        // Act
        var headersScope = logger.BeginWebhookScope(message.Headers);
        var webhookScope = logger.BeginWebhookScope(message.Headers, message.Event);

        // Assert
        headersScope.ShouldBeNull();
        webhookScope.ShouldBeNull();

        logger.DidNotReceiveWithAnyArgs().BeginScope<Dictionary<string, object>>(default!);
    }

    [Fact]
    public static void BeginWebhookScope_Creates_Scope_For_Headers()
    {
        // Arrange
        var (logger, scopes, scope) = CreateLogger();
        var message = CreateMessage();

        // Act
        var actual = logger.BeginWebhookScope(message.Headers);

        // Assert
        actual.ShouldBeSameAs(scope);

        var items = scopes.ShouldHaveSingleItem();

        items.Count.ShouldBe(2);
        items["GitHub.Delivery"].ShouldBe(message.Headers.Delivery);
        items["GitHub.Event"].ShouldBe("pull_request");
    }

    [Fact]
    public static void BeginWebhookScope_Creates_Single_Scope_For_Headers_And_Event()
    {
        // Arrange
        var (logger, scopes, scope) = CreateLogger();
        var message = CreateMessage();

        // Act
        var actual = logger.BeginWebhookScope(message.Headers, message.Event);

        // Assert
        actual.ShouldBeSameAs(scope);

        var items = scopes.ShouldHaveSingleItem();

        items["GitHub.Delivery"].ShouldBe(message.Headers.Delivery);
        items["GitHub.Event"].ShouldBe("pull_request");
        items["GitHub.Action"].ShouldBe("opened");
        items.ShouldContainKey("GitHub.Repository.HtmlUrl");
        items.ShouldContainKey("GitHub.Repository.Name");
        items.ShouldContainKey("GitHub.Sender.Login");
        items.ShouldContainKey("GitHub.PullRequest.Number");
        items.ShouldContainKey("GitHub.PullRequest.HtmlUrl");
    }

    private static GitHubEvent CreateMessage()
    {
        var driver = PullRequestDriver.ForDependabot();
        var payload = driver.CreateWebhook("opened", installationId: 42);

        return GitHubFixtures.CreateEvent("pull_request", payload, installationId: 42);
    }

    private static (ILogger Logger, List<Dictionary<string, object>> Scopes, IDisposable Scope) CreateLogger()
    {
        var scope = Substitute.For<IDisposable>();
        var scopes = new List<Dictionary<string, object>>();

        var logger = Substitute.For<ILogger>();

        logger.IsEnabled(LogLevel.Critical).Returns(true);
        logger.BeginScope(Arg.Do<Dictionary<string, object>>(scopes.Add)).Returns(scope);

        return (logger, scopes, scope);
    }
}
