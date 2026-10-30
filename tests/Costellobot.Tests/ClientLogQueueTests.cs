// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public static class ClientLogQueueTests
{
    [Fact]
    public static async Task Enqueue_Writes_To_Channel_When_Clients_Are_Connected()
    {
        // Arrange
        var connections = new GitHubWebhookHubConnections();
        connections.Connected();

        var target = new ClientLogQueue(connections);
        var message = new ClientLogMessage() { Message = "Hello, world!" };

        // Act
        bool actual = target.Enqueue(message);

        // Assert
        actual.ShouldBeTrue();
        target.History().ShouldBe([message]);

        var dequeued = await target.DequeueAsync(TestContext.Current.CancellationToken);

        dequeued.ShouldBeSameAs(message);
    }

    [Fact]
    public static async Task Enqueue_Only_Adds_To_History_When_No_Clients_Are_Connected()
    {
        // Arrange
        var connections = new GitHubWebhookHubConnections();
        var target = new ClientLogQueue(connections);
        var message = new ClientLogMessage() { Message = "Hello, world!" };

        // Act
        bool actual = target.Enqueue(message);

        // Assert
        actual.ShouldBeTrue();
        target.History().ShouldBe([message]);

        // The message is already available if it was written to the channel, so the
        // timeout is only reached when the reader has nothing to read.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        var dequeued = await target.DequeueAsync(cts.Token);

        dequeued.ShouldBeNull();
    }

    [Fact]
    public static async Task Enqueue_Writes_To_Channel_Again_After_A_Client_Connects()
    {
        // Arrange
        var connections = new GitHubWebhookHubConnections();
        var target = new ClientLogQueue(connections);

        var before = new ClientLogMessage() { Message = "Before" };
        var after = new ClientLogMessage() { Message = "After" };

        // Act
        target.Enqueue(before);

        connections.Connected();

        target.Enqueue(after);

        // Assert
        target.History().ShouldBe([before, after]);

        var dequeued = await target.DequeueAsync(TestContext.Current.CancellationToken);

        dequeued.ShouldBeSameAs(after);
    }

    [Fact]
    public static void Enqueue_Retains_Only_The_Most_Recent_History_When_No_Clients_Are_Connected()
    {
        // Arrange
        var connections = new GitHubWebhookHubConnections();
        var target = new ClientLogQueue(connections);

        var messages = Enumerable.Range(0, 25)
            .Select((p) => new ClientLogMessage() { Message = p.ToString(System.Globalization.CultureInfo.InvariantCulture) })
            .ToArray();

        // Act
        foreach (var message in messages)
        {
            target.Enqueue(message);
        }

        // Assert
        target.History().ShouldBe([.. messages.Skip(5)]);
    }
}
