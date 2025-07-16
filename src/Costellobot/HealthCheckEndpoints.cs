// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

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
               .RequireHost(["localhost", "127.0.0.1"])
               .RequireAuthorization((policy) =>
               {
                   policy.RequireAssertion((context) =>
                   {
                       if (context.Resource is not HttpContext httpContext)
                       {
                           return false;
                       }

                       var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();

                       // See https://learn.microsoft.com/azure/app-service/monitor-instances-health-check?tabs=dotnet#authentication-and-security
                       var token = httpContext.Request.Headers["x-ms-auth-internal-token"].FirstOrDefault();

                       if (token is { Length: > 0 })
                       {
                           var key = configuration["WEBSITE_AUTH_ENCRYPTION_KEY"];
                           var keyBytes = Encoding.UTF8.GetBytes(key ?? string.Empty);
                           var hashBytes = SHA256.HashData(keyBytes);
                           var hash = Convert.ToBase64String(hashBytes);

                           if (string.Equals(token, hash, StringComparison.Ordinal))
                           {
                               return true;
                           }
                       }

                       if (context.User.Identity?.IsAuthenticated is true)
                       {
                           var options = httpContext.RequestServices.GetRequiredService<IOptions<SiteOptions>>().Value;

                           bool hasClaim = false;
                           bool needsClaim = false;

                           if (options.AdminUsers is { Count: > 0 } users)
                           {
                               needsClaim = true;

                               foreach (var name in users)
                               {
                                   if (context.User.HasClaim(ClaimTypes.Name, name))
                                   {
                                       hasClaim = true;
                                       break;
                                   }
                               }
                           }

                           bool hasRole = false;
                           bool needsRole = false;

                           if (options.AdminRoles is { Count: > 0 } roles)
                           {
                               needsRole = true;

                               foreach (var role in roles)
                               {
                                   if (context.User.IsInRole(role))
                                   {
                                       hasRole = true;
                                       break;
                                   }
                               }
                           }

                           bool authorized = false;

                           if (needsClaim && needsRole)
                           {
                               authorized = hasClaim && hasRole;
                           }
                           else if (needsClaim && hasClaim)
                           {
                               authorized = (needsClaim && hasClaim) || (needsRole && hasRole);
                           }
                           else if (!needsClaim && !needsRole)
                           {
                               authorized = true;
                           }

                           if (authorized)
                           {
                               return true;
                           }
                       }

                       return false;
                   });
               });
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
