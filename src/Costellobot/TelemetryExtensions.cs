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
    public static void AddTelemetry(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var telemetry = builder.Services.AddOpenTelemetry();

        if (ApplicationTelemetry.IsOtlpCollectorConfigured())
        {
            telemetry.UseOtlpExporter();
        }

        telemetry
            .WithMetrics((metrics) =>
            {
                metrics.SetResourceBuilder(ApplicationTelemetry.ResourceBuilder)
                       .AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddProcessInstrumentation()
                       .AddMeter(ApplicationTelemetry.ServiceName)
                       .AddMeter("Microsoft.Extensions.Caching.Memory.MemoryCache")
                       .AddMeter("Microsoft.Extensions.Diagnostics.ResourceMonitoring")
                       .AddMeter("Polly")
                       .AddMeter("System.Runtime")
                       .SetExemplarFilter(ExemplarFilterType.TraceBased);
            })
            .WithTracing((tracing) =>
            {
                tracing.SetResourceBuilder(ApplicationTelemetry.ResourceBuilder)
                       .AddHttpClientInstrumentation()
                       .AddSource(ApplicationTelemetry.ServiceName)
                       .AddSource("Azure.*")
                       .AddSource("Microsoft.AspNetCore")
                       .AddSource("Microsoft.AspNetCore.SignalR.Server");

                if (builder.Environment.IsDevelopment())
                {
                    tracing.SetSampler(new AlwaysOnSampler());
                }

                if (ApplicationTelemetry.IsPyroscopeConfigured())
                {
                    tracing.AddProcessor(new Pyroscope.OpenTelemetry.PyroscopeSpanProcessor());
                }
            });

        builder.Services
            .AddOptions<HttpClientTraceInstrumentationOptions>()
            .Configure((options) =>
            {
                options.EnrichWithHttpResponseMessage = EnrichHttpActivity;
                options.RecordException = true;
            });

        builder.Services
            .AddOptions<AspNetCoreTraceInstrumentationOptions>()
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

        builder.Logging.AddTelemetry();
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
