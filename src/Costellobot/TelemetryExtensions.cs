// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using AspNet.Security.OAuth.GitHub;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Metrics;
using OpenTelemetry.ResourceDetectors.Azure;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MartinCostello.Costellobot;

public static class TelemetryExtensions
{
    private static readonly ConcurrentDictionary<string, string> ServiceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["api.github.com"] = "GitHub",
        ["github.com"] = "GitHub",
        ["raw.githubusercontent.com"] = "GitHub",
    };

    public static void AddTelemetry(this IServiceCollection services, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(ApplicationTelemetry.ServiceName, serviceVersion: ApplicationTelemetry.ServiceVersion)
            .AddDetector(new AppServiceResourceDetector());

        var telemetry = services
            .AddOpenTelemetry()
            .WithMetrics((builder) =>
            {
                builder.SetResourceBuilder(resourceBuilder)
                       .AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddRuntimeInstrumentation();

                if (IsOtlpCollectorConfigured())
                {
                    builder.AddOtlpExporter();
                }
            })
            .WithTracing((builder) =>
            {
                builder.SetResourceBuilder(resourceBuilder)
                       .AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddSource(ApplicationTelemetry.ServiceName);

                if (environment.IsDevelopment())
                {
                    builder.SetSampler(new AlwaysOnSampler());
                }

                if (IsOtlpCollectorConfigured())
                {
                    builder.AddOtlpExporter();
                }
            });

        if (IsAzureMonitorConfigured())
        {
            telemetry.UseAzureMonitor();
        }

        services.AddOptions<HttpClientTraceInstrumentationOptions>()
                .Configure<IServiceProvider>((options, provider) =>
                {
                    AddServiceMappings(ServiceMap, provider);

                    options.EnrichWithHttpRequestMessage = EnrichHttpActivity;
                    options.RecordException = true;
                });
    }

    internal static bool IsOtlpCollectorConfigured()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

    internal static bool IsAzureMonitorConfigured()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING"));

    private static void EnrichHttpActivity(Activity activity, HttpRequestMessage request)
    {
        TryEnrichWithPeerService(activity);

        static void TryEnrichWithPeerService(Activity activity)
        {
            if (GetTag("server.address", activity.Tags) is { Length: > 0 } hostName)
            {
                if (!ServiceMap.TryGetValue(hostName, out var service))
                {
                    service = hostName;
                }

                activity.AddTag("peer.service", service);
            }
        }

        static string? GetTag(string name, IEnumerable<KeyValuePair<string, string?>> tags)
            => tags.FirstOrDefault((p) => p.Key == name).Value;
    }

    private static void AddServiceMappings(ConcurrentDictionary<string, string> mappings, IServiceProvider serviceProvider)
    {
        var webhook = serviceProvider.GetRequiredService<IOptions<WebhookOptions>>().Value;

        foreach ((string registry, var endpoint) in webhook.Registries)
        {
            mappings[endpoint.BaseAddress.Host] = registry;
        }

        var github = serviceProvider.GetRequiredService<IOptions<GitHubOptions>>().Value;
        var oauth = serviceProvider.GetRequiredService<IOptions<GitHubAuthenticationOptions>>().Value;

        AddMapping("GitHub", github.EnterpriseDomain);
        AddMapping("GitHub", oauth.AuthorizationEndpoint);
        AddMapping("GitHub", oauth.TokenEndpoint);
        AddMapping("GitHub", oauth.UserInformationEndpoint);

        void AddMapping(string name, string? host)
        {
            if (host is { Length: > 0 } url &&
                Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                !mappings.ContainsKey(uri.Host))
            {
                mappings[uri.Host] = name;
            }
        }
    }
}
