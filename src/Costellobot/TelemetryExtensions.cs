﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using AspNet.Security.OAuth.GitHub;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Metrics;
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

    public static ResourceBuilder ResourceBuilder { get; } = ResourceBuilder.CreateDefault()
        .AddService(ApplicationTelemetry.ServiceName, serviceVersion: ApplicationTelemetry.ServiceVersion)
        .AddAzureAppServiceDetector()
        .AddContainerDetector()
        .AddOperatingSystemDetector()
        .AddProcessRuntimeDetector();

    public static void AddTelemetry(this IServiceCollection services, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (IsAzureMonitorConfigured())
        {
            services.Configure<AzureMonitorExporterOptions>(
                (p) => p.ConnectionString = AzureMonitorConnectionString());
        }

        services
            .AddOpenTelemetry()
            .WithMetrics((builder) =>
            {
                builder.SetResourceBuilder(ResourceBuilder)
                       .AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddProcessInstrumentation()
                       .AddRuntimeInstrumentation();

                if (IsAzureMonitorConfigured())
                {
                    builder.AddAzureMonitorMetricExporter();
                }

                if (IsOtlpCollectorConfigured())
                {
                    builder.AddOtlpExporter();
                }
            })
            .WithTracing((builder) =>
            {
                // See https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/Azure.Core/samples/Diagnostics.md
                AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

                builder.SetResourceBuilder(ResourceBuilder)
                       .AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddSource(ApplicationTelemetry.ServiceName)
                       .AddSource("Azure.*")
                       .AddSource("Microsoft.AspNetCore.SignalR.Server");

                if (environment.IsDevelopment())
                {
                    builder.SetSampler(new AlwaysOnSampler());
                }

                if (IsAzureMonitorConfigured())
                {
                    builder.AddAzureMonitorTraceExporter();
                }

                if (IsOtlpCollectorConfigured())
                {
                    builder.AddOtlpExporter();
                }
            });

        services.AddOptions<HttpClientTraceInstrumentationOptions>()
                .Configure<IServiceProvider>((options, provider) =>
                {
                    AddServiceMappings(ServiceMap, provider);

                    options.EnrichWithHttpRequestMessage = EnrichHttpActivity;
                    options.EnrichWithHttpResponseMessage = EnrichHttpActivity;

                    options.RecordException = true;
                });

        services.AddOptions<AspNetCoreTraceInstrumentationOptions>()
                .Configure<IServiceProvider>((options, provider) =>
                {
                    options.EnrichWithHttpResponse = static (activity, response) =>
                    {
                        if (response.StatusCode is StatusCodes.Status404NotFound)
                        {
                            activity.SetStatus(ActivityStatusCode.Ok);
                        }
                    };
                });
    }

    internal static bool IsOtlpCollectorConfigured()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

    internal static bool IsAzureMonitorConfigured()
        => !string.IsNullOrEmpty(AzureMonitorConnectionString());

    private static string? AzureMonitorConnectionString()
        => Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

    private static void EnrichHttpActivity(Activity activity, HttpRequestMessage request)
    {
        if (GetTag("server.address", activity.Tags) is { Length: > 0 } hostName)
        {
            if (!ServiceMap.TryGetValue(hostName, out var service))
            {
                service = hostName;
            }

            activity.AddTag("peer.service", service);
        }

        static string? GetTag(string name, IEnumerable<KeyValuePair<string, string?>> tags)
            => tags.FirstOrDefault((p) => p.Key == name).Value;
    }

    private static void EnrichHttpActivity(Activity activity, HttpResponseMessage response)
    {
        if (response.RequestMessage?.Headers.TryGetValues("x-ms-client-request-id", out var clientRequestId) is true)
        {
            activity.SetTag("az.client_request_id", clientRequestId);
        }

        if (response.Headers.TryGetValues("x-ms-request-id", out var requestId))
        {
            activity.SetTag("az.service_request_id", requestId);
        }
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
