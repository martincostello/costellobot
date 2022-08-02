// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public class WebhookDeliveryBuilder : ResponseBuilder
{
    public WebhookDeliveryBuilder(
        string @event,
        string? action = null,
        long? installationId = null,
        long? repositoryId = null)
    {
        Event = @event;
        Action = action;
        InstallationId = installationId;
        RepositoryId = repositoryId;
    }

    public Guid Guid { get; set; } = Guid.NewGuid();

    public DateTimeOffset DeliveredAt { get; set; } = DateTimeOffset.UtcNow;

    public bool Redelivery { get; set; }

#pragma warning disable CA5394
    public double Duration { get; set; } = Random.Shared.NextDouble();
#pragma warning restore CA5394

    public string Status { get; set; } = "OK";

    public int StatusCode { get; set; } = 200;

    public string Event { get; set; }

    public string? Action { get; set; }

    public long? InstallationId { get; set; }

    public long? RepositoryId { get; set; }

    public WebhookPayloadBuilder AsPayload()
    {
        return new(Event, Action, InstallationId, RepositoryId)
        {
            DeliveredAt = DeliveredAt,
            Duration = Duration,
            Guid = Guid,
            Id = Id,
            Redelivery = Redelivery,
            Status = Status,
            StatusCode = StatusCode,
        };
    }

    public WebhookDeliveryBuilder AsRedelivery()
    {
        return new(Event, Action, null, null)
        {
            DeliveredAt = DeliveredAt,
            Guid = Guid,
            Id = Id,
            Redelivery = true,
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
        };
    }
}
