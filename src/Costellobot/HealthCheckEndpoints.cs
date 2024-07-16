// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MartinCostello.Costellobot;

public static class HealthCheckEndpoints
{
    private const string LiveTag = "live";

    public static IServiceCollection AddApplicationHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);

        return services;
    }

    public static IEndpointRouteBuilder MapHealthCheckRoutes(this IEndpointRouteBuilder builder)
    {
        builder.MapHealthCheck("/health/readiness");
        builder.MapHealthCheck("/health/startup");
        builder.MapHealthCheck("/health/liveness", (p) => p.Predicate = (r) => r.Tags.Contains(LiveTag));

        return builder;
    }

    private static void MapHealthCheck(this IEndpointRouteBuilder builder, string pattern, Action<HealthCheckOptions>? configure = null)
    {
        var options = new HealthCheckOptions() { ResponseWriter = WriteResponse };

        configure?.Invoke(options);

        builder.MapHealthChecks(pattern, options)
               .RequireHost(["localhost", "127.0.0.1"]);
    }

    private static Task WriteResponse(HttpContext context, HealthReport healthReport)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new() { Indented = true }))
        {
            writer.WriteStartObject();
            {
                writer.WriteString("status", healthReport.Status.ToString());
                writer.WriteStartObject("results");

                foreach ((var name, var entry) in healthReport.Entries)
                {
                    writer.WriteStartObject(name);
                    {
                        writer.WriteString("status", entry.Status.ToString());

                        if (entry.Description is { Length: > 0 } description)
                        {
                            writer.WriteString("description", description);
                        }

                        writer.WriteStartObject("data");

                        foreach ((var key, var item) in entry.Data)
                        {
                            writer.WritePropertyName(key);
                            JsonSerializer.Serialize(writer, item, item?.GetType() ?? typeof(object));
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(Encoding.UTF8.GetString(stream.ToArray()));
    }
}
