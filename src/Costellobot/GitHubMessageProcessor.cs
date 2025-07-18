﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubMessageProcessor(
    IServiceProvider serviceProvider,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<GitHubMessageProcessor> logger) : WebhookEventProcessor
{
    public override async ValueTask ProcessWebhookAsync(
        IDictionary<string, StringValues> headers,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(body);

        if (options.CurrentValue.Disable)
        {
            return;
        }

        (var rawHeaders, var rawPayload) = ParseRaw(headers, body);

        var webhookHeaders = WebhookHeaders.Parse(headers);

        using (logger.BeginWebhookScope(webhookHeaders))
        {
            var webhookEvent = DeserializeWebhookEvent(webhookHeaders, body);

            using (logger.BeginWebhookScope(webhookEvent))
            {
                Log.ReceivedWebhook(logger, webhookHeaders.Delivery);
                await ProcessAsync(new(webhookHeaders, webhookEvent, rawHeaders, rawPayload), cancellationToken);
            }
        }
    }

    private static (IDictionary<string, string> Headers, JsonElement Payload) ParseRaw(
        IDictionary<string, StringValues> headers,
        string body)
    {
        // Cannot serialize the parsed webhook objects as-is because DateTimeOffset does not support being serialized and throws an exception.
        // See https://github.com/octokit/webhooks.net/blob/1a6ce29f8312c555227703057ba45723e3c78574/src/Octokit.Webhooks/Converter/DateTimeOffsetConverter.cs#L14.
        using var document = JsonDocument.Parse(body);

        var webhookHeaders = new Dictionary<string, string>(headers.Count);

        foreach ((var key, var values) in headers)
        {
            webhookHeaders[key] = values.ToString();
        }

        return (webhookHeaders, document.RootElement.Clone());
    }

    private async Task ProcessAsync(GitHubEvent message, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();

            var dispatcher = scope.ServiceProvider.GetRequiredService<GitHubWebhookDispatcher>();
            await dispatcher.DispatchAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.ProcessingFailed(logger, ex, message.Headers.Delivery);
            throw;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Received webhook with ID {HookId}.")]
        public static partial void ReceivedWebhook(ILogger logger, string? hookId);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Error,
            Message = "Failed to process webhook with ID {HookId}.")]
        public static partial void ProcessingFailed(ILogger logger, Exception exception, string? hookId);
    }
}
