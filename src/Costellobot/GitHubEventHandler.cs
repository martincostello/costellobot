// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using OpenTelemetry;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubEventHandler(
    ServiceBusClient client,
    GitHubWebhookQueue queue,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<GitHubEventHandler> logger)
{
    public async Task HandleAsync(GitHubEvent payload)
    {
        var config = options.CurrentValue;

        using var cts = new CancellationTokenSource(config.PublishTimeout);

        try
        {
            if (WellKnownGitHubEvents.IsKnown(payload))
            {
                var message = GitHubMessageSerializer.Serialize(payload.Headers.Delivery, payload.RawHeaders, payload.RawPayload.ToString());

                Baggage.SetBaggage(
                [
                    KeyValuePair.Create<string, string?>("github.webhook.delivery", payload.Headers.Delivery),
                    KeyValuePair.Create<string, string?>("github.webhook.event", payload.Headers.Event),
                    KeyValuePair.Create<string, string?>("github.webhook.event.action", payload.Event?.Action),
                    KeyValuePair.Create<string, string?>("messaging.message.id", message.MessageId),
                ]);

                var sender = client.CreateSender(config.QueueName);
                await sender.SendMessageAsync(message, cts.Token);
            }
            else
            {
                Log.IgnoringEvent(logger, payload.Headers.Delivery, payload.Headers.Event, payload.Event?.Action);
            }
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

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Debug,
           Message = "Ignoring GitHub webhook with ID {HookId} for event {Event}:{Action}.")]
        public static partial void IgnoringEvent(ILogger logger, string? hookId, string? @event, string? action);
    }
}
