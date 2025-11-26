// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace MartinCostello.Costellobot;

public static class TelemetryExtensions
{
    public static void AddTelemetry(this IServiceCollection services, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddOpenTelemetry();

        if (ApplicationTelemetry.IsOtlpCollectorConfigured())
        {
            builder.UseOtlpExporter();
        }

        builder.WithMetrics((builder) =>
               {
                   builder.SetResourceBuilder(ApplicationTelemetry.ResourceBuilder)
                          .AddAspNetCoreInstrumentation()
                          .AddHttpClientInstrumentation()
                          .AddProcessInstrumentation()
                          .AddMeter(ApplicationTelemetry.ServiceName)
                          .AddMeter("Polly")
                          .AddMeter("System.Runtime")
                          .SetExemplarFilter(ExemplarFilterType.TraceBased);
               })
               .WithTracing((builder) =>
               {
                   builder.SetResourceBuilder(ApplicationTelemetry.ResourceBuilder)
                          .AddAspNetCoreInstrumentation()
                          .AddHttpClientInstrumentation()
                          .AddSource(ApplicationTelemetry.ServiceName)
                          .AddSource("Azure.*")
                          .AddSource("Microsoft.AspNetCore.SignalR.Server");

                   if (environment.IsDevelopment())
                   {
                       builder.SetSampler(new AlwaysOnSampler());
                   }

                   if (ApplicationTelemetry.IsPyroscopeConfigured())
                   {
                       builder.AddProcessor(new Pyroscope.OpenTelemetry.PyroscopeSpanProcessor());
                   }
               });

        services.AddOptions<HttpClientTraceInstrumentationOptions>()
                .Configure((options) =>
                {
                    options.EnrichWithHttpRequestMessage = EnrichHttpActivity;
                    options.EnrichWithHttpResponseMessage = EnrichHttpActivity;
                    options.RecordException = true;
                });

        services.AddOptions<AspNetCoreTraceInstrumentationOptions>()
                .Configure((options) =>
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

    private static void EnrichHttpActivity(Activity activity, HttpRequestMessage request)
    {
        if (GetTag("server.address", activity.Tags) is { Length: > 0 } hostName)
        {
            activity.AddTag("peer.service", hostName);
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
}
