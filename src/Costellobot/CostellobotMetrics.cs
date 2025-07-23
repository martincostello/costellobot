// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.Metrics;

namespace MartinCostello.Costellobot;

public sealed class CostellobotMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _webhookDeliveriesCounter;

    public CostellobotMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(ApplicationTelemetry.ServiceName, ApplicationTelemetry.ServiceVersion);

        _webhookDeliveriesCounter = _meter.CreateCounter<long>(
            "costellobot.github.webhook.delivery",
            unit: "{count}",
            description: "The number of GitHub webhook deliveries received.");
    }

    public void Dispose() => _meter?.Dispose();

    public void WebhookDelivery(string? @event, string? targetId)
        => _webhookDeliveriesCounter.Add(
               1,
               new KeyValuePair<string, object?>("github.webhook.event", @event),
               new KeyValuePair<string, object?>("github.webhook.hook.installation.target.id", targetId));
}
