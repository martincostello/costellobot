// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Humanizer;

namespace MartinCostello.Costellobot.Models;

public sealed class DeliveryModel(JsonElement delivery)
{
    public long Id => Delivery.GetProperty("id").GetInt64();

    public string Action => Delivery.GetProperty("action").GetString() ?? "-";

    public string DeliveryId => Delivery.GetProperty("guid").GetString() ?? "-";

    public DateTimeOffset DeliveredAt => Delivery.GetProperty("delivered_at").GetDateTimeOffset();

    public string Duration => Delivery
        .GetProperty("duration")
        .GetDouble()
        .Seconds()
        .TotalSeconds
        .ToString("N2", CultureInfo.InvariantCulture);

    public string Event => Delivery.GetProperty("event").GetString() ?? "-";

    public bool Redelivery => Delivery.GetProperty("redelivery").GetBoolean();

    public string RepositoryId { get; set; } = "-";

    public IDictionary<string, string> RequestHeaders { get; } = new Dictionary<string, string>();

    public string RequestPayload { get; set; } = string.Empty;

    public string RequestUrl => Delivery.GetProperty("url").GetString() ?? "-";

    public IDictionary<string, string> ResponseHeaders { get; } = new Dictionary<string, string>();

    public string ResponseBody { get; set; } = string.Empty;

    public string? ResponseStatus => Delivery.GetProperty("status").GetString();

    public int ResponseStatusCode => Delivery.GetProperty("status_code").GetInt32();

    private JsonElement Delivery { get; set; } = delivery;
}
