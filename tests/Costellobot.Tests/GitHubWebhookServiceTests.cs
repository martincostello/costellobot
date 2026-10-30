// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;
using NSubstitute;

namespace MartinCostello.Costellobot;

public class GitHubWebhookServiceTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    public async Task StartAsync_Configures_The_Concurrency_Of_The_Processor(int maxConcurrentCalls, int expected)
    {
        // Arrange
        var options = new WebhookOptions()
        {
            MaxConcurrentCalls = maxConcurrentCalls,
            QueueName = "webhooks",
        }.ToMonitor();

        ServiceBusProcessorOptions? actual = null;

        var serviceProvider = Substitute.For<IServiceProvider>();

        await using var client = Substitute.For<ServiceBusClient>();
        await using var serviceBusProcessor = Substitute.For<ServiceBusProcessor>();

        client.CreateProcessor("webhooks", Arg.Do<ServiceBusProcessorOptions>((p) => actual = p))
              .Returns(serviceBusProcessor);

        var processor = new GitHubMessageProcessor(
            serviceProvider,
            options,
            outputHelper.ToLogger<GitHubMessageProcessor>());

        await using var target = new GitHubWebhookService(
            client,
            processor,
            options,
            outputHelper.ToLogger<GitHubWebhookService>());

        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await target.StartAsync(cancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.MaxConcurrentCalls.ShouldBe(expected);

        await target.StopAsync(cancellationToken);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("application/octet-stream", "github-webhook")]
    [InlineData("application/json", "foo")]
    public async Task ProcessAsync_Validates_Message(string contentType, string subject)
    {
        // Arrange
        var body = AmqpMessageBody.FromValue(string.Empty);
        var amqp = new AmqpAnnotatedMessage(body);

        amqp.Properties.ContentType = contentType;
        amqp.Properties.Subject = subject;

        var message = ServiceBusReceivedMessage.FromAmqpMessage(amqp, BinaryData.FromBytes([]));

        var options = new WebhookOptions().ToMonitor();
        var serviceProvider = Substitute.For<IServiceProvider>();

        await using var client = Substitute.For<ServiceBusClient>();
        await using var receiver = Substitute.For<ServiceBusReceiver>();

        var processor = new GitHubMessageProcessor(
            serviceProvider,
            options,
            outputHelper.ToLogger<GitHubMessageProcessor>());

        var target = new GitHubWebhookService(
            client,
            processor,
            options,
            outputHelper.ToLogger<GitHubWebhookService>());

        var args = new ProcessMessageEventArgs(
            message,
            receiver,
            TestContext.Current.CancellationToken);

        // Act and Assert
        await Should.ThrowAsync<InvalidOperationException>(() => target.ProcessAsync(args));
    }
}
