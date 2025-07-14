// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubEventHandler(
    ServiceBusClient client,
    GitHubWebhookQueue queue,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<GitHubEventHandler> logger)
{
    public async Task HandleAsync(GitHubEvent payload, CancellationToken cancellationToken)
    {
        if (Activity.Current is { } activity)
        {
            activity.SetTag("github.webhook.delivery", payload.Headers.Delivery);
            activity.SetTag("github.webhook.hook.id", payload.Headers.HookId);
            activity.SetTag("github.webhook.hook.installation.target.id", payload.Headers.HookInstallationTargetId);
            activity.SetTag("github.webhook.hook.installation.target.type", payload.Headers.HookInstallationTargetType);
            activity.SetTag("github.webhook.event", payload.Headers.Event);
            activity.SetTag("github.webhook.payload.action", payload.Event?.Action);
        }

        var config = options.CurrentValue;

        using var timeout = new CancellationTokenSource(config.PublishTimeout);
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            if (WellKnownGitHubEvents.IsKnown(payload))
            {
                var message = GitHubMessageSerializer.Serialize(payload.Headers.Delivery, payload.RawHeaders, payload.RawPayload.ToString());

                if (message is null)
                {
                    Log.PublishSkippedPayloadTooLarge(logger, payload.Headers.Delivery);
                }
                else
                {
                    Activity.Current?.SetTag("messaging.message.conversation_id", message.CorrelationId);
                    Activity.Current?.SetTag("messaging.message.id", message.MessageId);

                    var sender = client.CreateSender(config.QueueName);
                    await sender.SendMessageAsync(message, combined.Token);
                }
            }
            else
            {
                Log.IgnoringEvent(logger, payload.Headers.Delivery, payload.Headers.Event, payload.Event?.Action);
            }
        }
        catch (ServiceBusException ex) when (ex.Reason is ServiceBusFailureReason.MessageSizeExceeded)
        {
            Log.PublishFailedPayloadTooLarge(logger, ex, payload.Headers.Delivery);
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

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Cannot publish message for webhook with ID {HookId} as the payload is too large.")]
        public static partial void PublishSkippedPayloadTooLarge(ILogger logger, string? hookId);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "Failed to publish message for webhook with ID {HookId} as the payload is too large.")]
        public static partial void PublishFailedPayloadTooLarge(ILogger logger, Exception exception, string? hookId);
    }
}
