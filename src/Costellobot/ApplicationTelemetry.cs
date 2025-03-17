// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using Pyroscope;

namespace MartinCostello.Costellobot;

public static class ApplicationTelemetry
{
    public static readonly string ServiceName = "Costellobot";
    public static readonly string ServiceVersion = GitMetadata.Version.Split('+')[0];
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    public static ResourceBuilder ResourceBuilder { get; } = ResourceBuilder.CreateDefault()
        .AddService(ServiceName, serviceVersion: ServiceVersion)
        .AddAzureAppServiceDetector()
        .AddContainerDetector()
        .AddOperatingSystemDetector()
        .AddProcessRuntimeDetector();

    internal static bool IsOtlpCollectorConfigured()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

    internal static bool IsPyroscopeConfigured()
        => Environment.GetEnvironmentVariable("PYROSCOPE_PROFILING_ENABLED") is "1";

    internal static async Task ExecuteWithProfilerAsync<T>(T state, Func<T, Task> operation)
    {
        if (ExtractK6Baggage() is not { Count: > 0 } baggage)
        {
            await operation(state);
            return;
        }

        try
        {
            Profiler.Instance.ClearDynamicTags();

            foreach ((string key, string value) in baggage)
            {
                Profiler.Instance.SetDynamicTag(key, value);
            }

            await operation(state);
        }
        finally
        {
            Profiler.Instance.ClearDynamicTags();
        }
    }

    private static Dictionary<string, string>? ExtractK6Baggage()
    {
        if (Baggage.GetBaggage() is not { Count: > 0 } baggage)
        {
            return null;
        }

        Dictionary<string, string>? labels = null;

        foreach ((string key, string? value) in baggage.Where((p) => p.Key.StartsWith("k6.", StringComparison.Ordinal)))
        {
            if (value is { Length: > 0 })
            {
                string label = key.Replace('.', '_');

                // See https://grafana.com/docs/k6/latest/javascript-api/jslib/http-instrumentation-pyroscope/#about-baggage-header
                labels ??= new(3);
                labels[label] = value;
            }
        }

        return labels;
    }
}
