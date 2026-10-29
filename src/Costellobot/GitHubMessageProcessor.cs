// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

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

        var rawHeaders = ParseRawHeaders(headers);

        var webhookHeaders = WebhookHeaders.Parse(headers);

        var webhookEvent = DeserializeWebhookEvent(webhookHeaders, body);

        using (logger.BeginWebhookScope(webhookHeaders, webhookEvent))
        {
            Log.ReceivedWebhook(logger, webhookHeaders.Delivery);
            await ProcessAsync(new(webhookHeaders, webhookEvent, rawHeaders, body), cancellationToken);
        }
    }

    private static Dictionary<string, string> ParseRawHeaders(IDictionary<string, StringValues> headers)
    {
        var webhookHeaders = new Dictionary<string, string>(headers.Count);

        foreach ((var key, var values) in headers)
        {
            webhookHeaders[key] = values.ToString();
        }

        return webhookHeaders;
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
