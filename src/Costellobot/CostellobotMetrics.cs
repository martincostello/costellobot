// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.Metrics;

namespace MartinCostello.Costellobot;

public sealed class CostellobotMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _webhookDeliveriesCounter;
    private readonly Counter<long> _tokenIssued;

    public CostellobotMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(ApplicationTelemetry.ServiceName, ApplicationTelemetry.ServiceVersion);

        _webhookDeliveriesCounter = _meter.CreateCounter<long>(
            "costellobot.github.webhook.delivery",
            unit: "{count}",
            description: "The number of GitHub webhook deliveries received.");

        _tokenIssued = _meter.CreateCounter<long>(
            "costellobot.github.token.issued",
            unit: "{count}",
            description: "The number of GitHub tokens issued.");
    }

    public void Dispose() => _meter?.Dispose();

    public void WebhookDelivery(string? @event, string? targetId)
        => _webhookDeliveriesCounter.Add(
               1,
               new("github.webhook.event", @event),
               new("github.webhook.hook.installation.target.id", targetId));

    public void TokenIssued(string repository, string profile)
        => _tokenIssued.Add(
               1,
               new("github.repository", repository),
               new("github.token.profile", profile));
}
