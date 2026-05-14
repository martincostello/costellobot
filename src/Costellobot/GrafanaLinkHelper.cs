// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot;

internal sealed class GrafanaLinkHelper(IConfiguration configuration, IOptionsMonitor<GrafanaOptions> options)
{
    public void AddTelemetryLinks(DeliveryModel model)
    {
        var grafanaUrl = options.CurrentValue.Url;

        if (string.IsNullOrEmpty(grafanaUrl))
        {
            return;
        }

        var serviceName = configuration["WEBSITE_SITE_NAME"] ?? "costellobot-martincostello"; // ApplicationTelemetry.ServiceName;

        // Round deliveredAt to the previous minute
        var deliveredAt = model.DeliveredAt.UtcDateTime;
        deliveredAt = new(deliveredAt.Ticks - (deliveredAt.Ticks % TimeSpan.TicksPerMinute), deliveredAt.Kind);

        // Query +/- 5 minutes around the rounded delivery time
        const string GrafanaTimestampFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK";
        var window = TimeSpan.FromMinutes(5);
        var from = deliveredAt.Add(-window).ToString(GrafanaTimestampFormat, CultureInfo.InvariantCulture);
        var to = deliveredAt.Add(window).ToString(GrafanaTimestampFormat, CultureInfo.InvariantCulture);

        var urlBuilder = new UriBuilder(grafanaUrl);

        const string FiltersParameter = "var-filters";
        const string MetadataParameter = "var-metadata";
        const string MetricsFiltersParameter = "var-metrics_filters";

        var parameters = new Dictionary<string, string?>(3)
        {
            ["from"] = from,
            ["to"] = to,
            ["timezone"] = "browser",
        };

        urlBuilder.Path = $"a/grafana-lokiexplore-app/explore/service/{serviceName}/logs";

        model.LogsUrl = QueryHelpers.AddQueryString(urlBuilder.ToString(), parameters);
        model.LogsUrl = QueryHelpers.AddQueryString(model.LogsUrl, FiltersParameter, $"service_name|=|{serviceName}");
        model.LogsUrl = QueryHelpers.AddQueryString(model.LogsUrl, MetadataParameter, $"GitHub_Delivery|=|{model.DeliveryId}");

        urlBuilder.Path = "a/grafana-exploretraces-app/explore";

        model.TracesUrl = QueryHelpers.AddQueryString(urlBuilder.ToString(), parameters);
        model.TracesUrl = QueryHelpers.AddQueryString(model.TracesUrl, FiltersParameter, $"resource.service.name|=|{serviceName}");
        model.TracesUrl = QueryHelpers.AddQueryString(model.TracesUrl, FiltersParameter, $"span.github.webhook.delivery|=|{model.DeliveryId}");
        model.TracesUrl = QueryHelpers.AddQueryString(model.TracesUrl, MetadataParameter, $"GitHub_Delivery|=|{model.DeliveryId}");

        urlBuilder.Path = "a/grafana-metricsdrilldown-app/drilldown";

        model.MetricsUrl = QueryHelpers.AddQueryString(urlBuilder.ToString(), parameters);
        model.MetricsUrl = QueryHelpers.AddQueryString(model.MetricsUrl, FiltersParameter, $"github_webhook_event|=|{model.Event}");
        model.MetricsUrl = QueryHelpers.AddQueryString(model.MetricsUrl, FiltersParameter, $"service_name|=|{serviceName}");
        model.MetricsUrl = QueryHelpers.AddQueryString(model.MetricsUrl, MetricsFiltersParameter, $"service_name|=|{serviceName}");
        model.MetricsUrl = QueryHelpers.AddQueryString(model.MetricsUrl, MetricsFiltersParameter, $"github_webhook_event|=|{model.Event}");

        urlBuilder.Path = "a/grafana-pyroscope-app/explore";

        model.ProfilesUrl = QueryHelpers.AddQueryString(urlBuilder.ToString(), parameters);
        model.ProfilesUrl = QueryHelpers.AddQueryString(model.ProfilesUrl, "explorationType", "flame-graph");
        model.ProfilesUrl = QueryHelpers.AddQueryString(model.ProfilesUrl, "var-serviceName", serviceName);
        model.ProfilesUrl = QueryHelpers.AddQueryString(model.ProfilesUrl, "var-profileMetricId", "process_cpu:cpu:nanoseconds:cpu:nanoseconds");
    }
}
