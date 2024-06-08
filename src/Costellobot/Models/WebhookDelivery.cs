// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace MartinCostello.Costellobot.Models;

public sealed class WebhookDelivery
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("guid")]
    public string Guid { get; set; } = string.Empty;

    [JsonPropertyName("delivered_at")]
    public DateTimeOffset DeliveredAt { get; set; }

    [JsonPropertyName("redelivery")]
    public bool IsRedelivery { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; }

    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("installation_id")]
    public long? InstallationId { get; set; }

    [JsonPropertyName("repository_id")]
    public long? RepositoryId { get; set; }
}
