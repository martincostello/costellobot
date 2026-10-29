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
    public static readonly string ServiceNamespace = "Costellobot";
    public static readonly string ServiceVersion = GitMetadata.Version.IndexOf('+', StringComparison.Ordinal) is >= 0 and var i
        ? GitMetadata.Version[..i]
        : GitMetadata.Version;

    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    public static ResourceBuilder ResourceBuilder { get; } = ResourceBuilder.CreateDefault()
        .AddService(ServiceName, ServiceNamespace, ServiceVersion)
        .AddAzureAppServiceDetector()
        .AddContainerDetector()
        .AddHostDetector()
        .AddOperatingSystemDetector()
        .AddProcessRuntimeDetector()
        .AddAttributes(
            [
                new("service.criticality", "high"),
                new("vcs.owner.name", GitMetadata.RepositoryOwner),
                new("vcs.provider.name", "github"),
                new("vcs.ref.head.name", GitMetadata.Branch),
                new("vcs.ref.head.revision", GitMetadata.Commit),
                new("vcs.ref.head.type", "branch"),
                new("vcs.repository.name", GitMetadata.RepositoryName),
                new("vcs.repository.url.full", GitMetadata.RepositoryUrl),
            ]);

    internal static bool IsOtlpCollectorConfigured()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

    internal static bool IsPyroscopeConfigured()
        => Environment.GetEnvironmentVariable("PYROSCOPE_PROFILING_ENABLED") is "1";

    [StackTraceHidden]
    internal static async Task ProfileAsync<T>(
        T state,
        Func<T, Task> operation)
    {
        var labelBuilder = GetProfilerLabels();

        try
        {
            labelBuilder.Build()
                        .Activate();

            await operation(state);
        }
        finally
        {
            Profiler.Instance.ClearDynamicTags();
        }
    }

    internal static bool HasK6Baggage()
    {
        var baggage = Baggage.Current;

        if (baggage.Count is 0)
        {
            return false;
        }

        foreach ((string key, _) in baggage)
        {
            if (key.StartsWith("k6.", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static LabelSet.Builder GetProfilerLabels()
    {
        var builder = LabelSet.Empty.BuildUpon();

        // Based on https://github.com/grafana/pyroscope-go/blob/8fff2bccb5ed5611fdb09fdbd9a727367ab35f39/x/k6/baggage.go
        foreach ((string key, string? value) in Baggage.Current)
        {
            if (value is { Length: > 0 } && key.StartsWith("k6.", StringComparison.Ordinal))
            {
                builder.Add(key.Replace('.', '_'), value);
            }
        }

        return builder;
    }
}
