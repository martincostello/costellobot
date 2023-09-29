// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public class WebhookPayloadBuilder(
    string @event,
    string? action = null,
    long? installationId = null,
    long? repositoryId = null) : WebhookDeliveryBuilder(@event, action, installationId, repositoryId)
{
    public IDictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

    public object RequestPayload { get; set; } = new { };

    public IDictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();

    public object ResponsePayload { get; set; } = string.Empty;

    public string Url { get; set; } = "https://costellobot.martincostello.local/github-webhook";

    public WebhookPayloadBuilder ForRedelivery()
    {
        return new(Event, Action, null, null)
        {
            DeliveredAt = DeliveredAt,
            Guid = Guid,
            Id = Id,
            Redelivery = true,
            RequestHeaders = RequestHeaders,
            RequestPayload = RequestPayload,
            ResponseHeaders = ResponseHeaders,
            ResponsePayload = ResponsePayload,
            Url = Url,
        };
    }

    public override object Build()
    {
        return new
        {
            id = Id,
            guid = Guid,
            delivered_at = DeliveredAt,
            redelivery = Redelivery,
            duration = Duration,
            status = Status,
            status_code = StatusCode,
            @event = Event,
            action = Action,
            installation_id = InstallationId,
            repository_id = RepositoryId,
            url = Url,
            request = new
            {
                headers = RequestHeaders,
                payload = RequestPayload,
            },
            response = new
            {
                headers = ResponseHeaders,
                payload = ResponsePayload,
            },
        };
    }
}
