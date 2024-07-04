// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot;

public sealed partial class MessagingGitHubEventHandler(
    ServiceBusClient client,
    GitHubWebhookQueue queue,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<MessagingGitHubEventHandler> logger) : IGitHubEventHandler
{
    public async Task HandleAsync(GitHubEvent payload, CancellationToken cancellationToken)
    {
        var config = options.CurrentValue;

        using var cts = new CancellationTokenSource(config.PublishTimeout);

        try
        {
            var message = GitHubMessageSerializer.Serialize(payload.Headers.Delivery, payload.RawHeaders, payload.RawPayload.ToString());

            var sender = client.CreateSender(config.QueueName);
            await sender.SendMessageAsync(message, cts.Token);
        }
        catch (Exception ex)
        {
            Log.PublishFailed(logger, ex, payload.Headers.Delivery);
            throw;
        }

        queue.Enqueue(payload);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Error,
           Message = "Failed to publish message for webhook with ID {HookId}.")]
        public static partial void PublishFailed(ILogger logger, Exception exception, string? hookId);
    }
}
