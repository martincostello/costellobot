// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;
using NSubstitute;

namespace MartinCostello.Costellobot;

public class MessagingGitHubJobTests(ITestOutputHelper outputHelper)
{
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
            outputHelper.ToLogger<GitHubMessageProcessor>());

        var target = new MessagingGitHubJob(
            client,
            processor,
            options,
            outputHelper.ToLogger<MessagingGitHubJob>());

        var args = new ProcessMessageEventArgs(
            message,
            receiver,
            CancellationToken.None);

        // Act and Assert
        await Should.ThrowAsync<InvalidOperationException>(() => target.ProcessAsync(args));
    }
}
