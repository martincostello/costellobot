// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public record GitHubEvent(
    WebhookHeaders Headers,
    WebhookEvent Event,
    IDictionary<string, string> RawHeaders,
    string Body)
{
    private JsonElement? _rawPayload;

    public GitHubEvent(
        WebhookHeaders headers,
        WebhookEvent @event,
        IDictionary<string, string> rawHeaders,
        string body,
        JsonElement rawPayload)
        : this(headers, @event, rawHeaders, body)
    {
        _rawPayload = rawPayload;
    }

    /// <summary>
    /// Gets the raw JSON payload of the webhook event.
    /// </summary>
    /// <remarks>
    /// The payload is parsed lazily from <see cref="Body"/> if it was not
    /// provided when the instance was created, as it is only needed when
    /// the event is broadcast to connected clients.
    /// </remarks>
    public JsonElement RawPayload
    {
        get
        {
            if (_rawPayload is not { } payload)
            {
                payload = JsonElement.Parse(Body);
                _rawPayload = payload;
            }

            return payload;
        }
    }

    public override string ToString()
    {
        var builder = new StringBuilder()
            .Append(Headers.Event);

        if (Event.Action is { Length: > 0 })
        {
            builder.Append(':')
                   .Append(Event.Action);
        }

        return builder.ToString();
    }
}
