// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using MartinCostello.Costellobot.Models;
using MartinCostello.Costellobot.Slices;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
using Octokit;
using Activity = System.Diagnostics.Activity;

namespace MartinCostello.Costellobot;

public static class AdminEndpoints
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    public static IEndpointRouteBuilder MapAdminRoutes(this IEndpointRouteBuilder builder)
    {
        builder.MapHub<GitHubWebhookHub>("/admin/git-hub");

        builder.MapMethods("/error", [HttpMethod.Get.Method, HttpMethod.Head.Method, HttpMethod.Post.Method], (HttpContext context, int? id = null) =>
        {
            int statusCode = id ?? StatusCodes.Status500InternalServerError;

            if (!Enum.IsDefined(typeof(HttpStatusCode), (HttpStatusCode)statusCode) ||
                id < StatusCodes.Status400BadRequest ||
                id > 599)
            {
                statusCode = StatusCodes.Status500InternalServerError;
            }

            var requestId = Activity.Current?.Id ?? context.TraceIdentifier;

            if (context.Request.IsJson())
            {
                var detail = ReasonPhrases.GetReasonPhrase(statusCode);
                var instance = context.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath ?? context.Request.Path;
                var extensions = new Dictionary<string, object?>(1) { ["correlation"] = requestId };

                return Results.Problem(detail, instance, statusCode, extensions: extensions);
            }

            var model = new ErrorModel(statusCode)
            {
                RequestId = requestId,
                Subtitle = $"Error (HTTP {statusCode})",
            };

            switch (statusCode)
            {
                case StatusCodes.Status400BadRequest:
                    model.Title = "Bad request";
                    model.Subtitle = "Bad request (HTTP 400)";
                    model.Message = "The request was invalid.";
                    model.IsClientError = true;
                    break;

                case StatusCodes.Status401Unauthorized:
                    model.Title = "Unauthorized";
                    model.Subtitle = "Unauthorized (HTTP 401)";
                    model.Message = "You must authenticate to access this page.";
                    model.IsClientError = true;
                    break;

                case StatusCodes.Status403Forbidden:
                    model.Title = "Forbidden";
                    model.Subtitle = "Forbidden (HTTP 403)";
                    model.Message = "You are not permitted to access this page.";
                    model.IsClientError = true;
                    break;

                case StatusCodes.Status404NotFound:
                    model.Title = "Not found";
                    model.Subtitle = "Page not found (HTTP 404)";
                    model.Message = "The page you requested could not be found.";
                    model.IsClientError = true;
                    break;

                case StatusCodes.Status405MethodNotAllowed:
                    model.Title = "Method not allowed";
                    model.Subtitle = "HTTP method not allowed (HTTP 405)";
                    model.Message = "The specified HTTP method was not allowed.";
                    model.IsClientError = true;
                    break;

                default:
                    break;
            }

            return Results.Extensions.RazorSlice<Error, ErrorModel>(model, statusCode);
        }).AllowAnonymous()
          .DisableAntiforgery()
          .WithMetadata(new ResponseCacheAttribute() { Duration = 0, Location = ResponseCacheLocation.None, NoStore = true });

        var admin = new Authorization.CostellobotAdminAttribute();

        builder
            .MapGet(
                "/configuration",
                async (
                    IGitHubClientFactory clientFactory,
                    IOptionsMonitor<GitHubOptions> github,
                    IOptionsMonitor<WebhookOptions> webhook) =>
                {
                    var options = github.CurrentValue;
                    var installationLimits = new Dictionary<string, MiscellaneousRateLimit?>();

                    foreach ((var id, var installation) in options.Installations)
                    {
                        var app = options.Apps[installation.AppId];
                        var installationClient = clientFactory.CreateForInstallation(id);
                        installationLimits[app.Name] = await GetRateLimitsAsync(installationClient);
                    }

                    var userClient = clientFactory.CreateForUser();
                    var userLimits = await GetRateLimitsAsync(userClient);

                    var model = new ConfigurationModel(options, webhook.CurrentValue, installationLimits, userLimits);
                    return Results.Extensions.RazorSlice<Configuration, ConfigurationModel>(model);

                    static async Task<MiscellaneousRateLimit?> GetRateLimitsAsync(IGitHubClient client)
                    {
                        try
                        {
                            return await client.RateLimit.GetRateLimits();
                        }
                        catch (Exception)
                        {
                            // Ignore
                            return null;
                        }
                    }
                })
            .WithName("Configuration")
            .WithMetadata(admin);

        builder.MapMethods("/", [HttpMethod.Get.Method, HttpMethod.Head.Method], () => Results.Extensions.RazorSlice<Home>())
               .WithMetadata(admin);

        const string DeliveryRoute = "Delivery";
        const string DeliveriesRoute = "Deliveries";

        builder
            .MapGet(
            "/deliveries/{app}",
            async (
                string app,
                IGitHubClientFactory factory,
                IOptionsMonitor<GitHubOptions> options) =>
            {
                if (options.CurrentValue.TryGetAppId(app) is not { } appId)
                {
                    return Results.NotFound();
                }

                var client = factory.CreateForApp(appId);

                (var deliveries, _) = await GetDeliveries(client, cursor: null);

                var model = new DeliveriesModel(app, deliveries);

                return Results.Extensions.RazorSlice<Deliveries, DeliveriesModel>(model);
            })
            .AddEndpointFilter<SetAntiforgeryCookieFilter>()
            .WithName(DeliveriesRoute)
            .WithMetadata(admin);

        builder.MapPost(
            "/deliveries/{app}",
            async (
                string app,
                [FromForm] Guid? id,
                IGitHubClientFactory factory,
                HttpContext context,
                IAntiforgery antiforgery,
                IOptionsMonitor<GitHubOptions> options) =>
            {
                if (options.CurrentValue.TryGetAppId(app) is not { } appId)
                {
                    return Results.NotFound();
                }

                var routeValues = new RouteValueDictionary() { ["app"] = app };

                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    antiforgery.SetCookieTokenAndHeader(context);
                    return Results.RedirectToRoute(DeliveriesRoute, routeValues);
                }

                const int MaxPages = 20;

                string? cursor = null;
                string? deliveryId = id?.ToString();

                if (deliveryId is { } guid)
                {
                    var client = factory.CreateForApp(appId);

                    for (int i = 0; i < MaxPages; i++)
                    {
                        (var deliveries, cursor) = await GetDeliveries(client, cursor);

                        var item = deliveries.FirstOrDefault((p) => p.Guid == guid);

                        if (item is not null)
                        {
                            routeValues["id"] = item.Id;
                            return Results.RedirectToRoute(DeliveryRoute, routeValues);
                        }
                    }
                }

                return Results.RedirectToRoute(DeliveriesRoute, routeValues);
            })
            .WithMetadata(admin);

        builder
            .MapGet("/delivery/{app}/{id}", async (
                string app,
                long id,
                IGitHubClientFactory factory,
                IOptionsMonitor<GitHubOptions> options) =>
            {
                if (options.CurrentValue.TryGetAppId(app) is not { } appId)
                {
                    return Results.NotFound();
                }

                // See https://docs.github.com/en/rest/apps/webhooks#get-a-delivery-for-an-app-webhook
                var uri = new Uri($"app/hook/deliveries/{id}", UriKind.Relative);

                IApiResponse<Stream> apiResponse;

                try
                {
                    var client = factory.CreateForApp(appId);
                    apiResponse = await client.Connection.GetRawStream(uri, null);
                }
                catch (NotFoundException)
                {
                    return Results.NotFound();
                }

                using var document = apiResponse switch
                {
                    { Body: Stream stream } => await JsonDocument.ParseAsync(stream),
                    { HttpResponse: Stream stream } => await JsonDocument.ParseAsync(stream),
                    _ => JsonDocument.Parse(apiResponse.HttpResponse.Body.ToString()!),
                };

                var delivery = document.RootElement;
                var model = new DeliveryModel(delivery.Clone());

                var request = delivery.GetProperty("request");

                TryPopulateHeaders(request.GetProperty("headers"), model.RequestHeaders);

                model.RequestPayload = JsonObject.Create(request.GetProperty("payload"))!.ToJsonString(IndentedOptions);

                var response = delivery.GetProperty("response");

                TryPopulateHeaders(response.GetProperty("headers"), model.ResponseHeaders);

                model.ResponseBody = response.GetProperty("payload").ToString();

                if (delivery.TryGetProperty("repository_id", out var repositoryId) &&
                    repositoryId.ValueKind != JsonValueKind.Null &&
                    repositoryId.TryGetInt64(out var value))
                {
                    model.RepositoryId = value.ToString(CultureInfo.InvariantCulture);
                }

                return Results.Extensions.RazorSlice<Delivery, DeliveryModel>(model);

                static void TryPopulateHeaders(JsonElement element, IDictionary<string, string> headers)
                {
                    if (element.ValueKind != JsonValueKind.Null)
                    {
                        foreach (var property in element.EnumerateObject().OrderBy((p) => p.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            headers[property.Name] = property.Value.ToString();
                        }
                    }
                }
            })
            .AddEndpointFilter<SetAntiforgeryCookieFilter>()
            .WithName(DeliveryRoute)
            .WithMetadata(admin);

        builder.MapPost(
            "/delivery/{app}/{id}",
            async (
                string app,
                long id,
                IGitHubClientFactory factory,
                HttpContext context,
                IAntiforgery antiforgery,
                IOptionsMonitor<GitHubOptions> options) =>
            {
                if (options.CurrentValue.TryGetAppId(app) is not { } appId)
                {
                    return Results.NotFound();
                }

                var client = factory.CreateForApp(appId);

                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    antiforgery.SetCookieTokenAndHeader(context);

                    var routeValues = new RouteValueDictionary()
                    {
                        ["app"] = app,
                        ["id"] = id,
                    };

                    return Results.RedirectToRoute(DeliveryRoute, routeValues);
                }

                // See https://docs.github.com/en/rest/apps/webhooks#redeliver-a-delivery-for-an-app-webhook
                var uri = new Uri($"app/hook/deliveries/{id}/attempts", UriKind.Relative);

                await client.Connection.Post(uri);

                return Results.RedirectToRoute(DeliveriesRoute, new() { ["app"] = app });
            })
            .AddEndpointFilter<SetAntiforgeryCookieFilter>()
            .WithMetadata(admin);

        const string DependenciesRoute = "Dependencies";

        builder
            .MapGet("/dependencies", async (ITrustStore store, CancellationToken cancellationToken) =>
            {
                DependencyEcosystem[] ecosystems =
                [
                    DependencyEcosystem.Docker,
                    DependencyEcosystem.GitHubActions,
                    DependencyEcosystem.Npm,
                    DependencyEcosystem.NuGet,
                    DependencyEcosystem.Ruby,
                ];

                var model = new Dictionary<DependencyEcosystem, IReadOnlyList<TrustedDependency>>();

                var comparer = Comparer<string>.Create(static (x, y) =>
                {
                    if (NuGetVersion.TryParse(x, out var versionX) &&
                        NuGetVersion.TryParse(y, out var versionY))
                    {
                        return versionX.CompareTo(versionY);
                    }

                    return string.Compare(x, y, StringComparison.Ordinal);
                });

                foreach (var ecosystem in ecosystems)
                {
                    var dependencies = await store.GetTrustAsync(ecosystem, cancellationToken);

                    model[ecosystem] = [.. dependencies
                        .OrderBy((p) => p.Id)
                        .ThenByDescending((p) => p.Version, comparer)];
                }

                return Results.Extensions.RazorSlice<Dependencies, IReadOnlyDictionary<DependencyEcosystem, IReadOnlyList<TrustedDependency>>>(model);
            })
            .AddEndpointFilter<SetAntiforgeryCookieFilter>()
            .WithName(DependenciesRoute)
            .WithMetadata(admin);

        builder.MapPost(
            "/dependencies/distrust",
            async (
                [FromForm] DependencyEcosystem ecosystem,
                [FromForm] string id,
                [FromForm] string version,
                ITrustStore store,
                HttpContext context,
                IAntiforgery antiforgery,
                CancellationToken cancellationToken) =>
            {
                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    antiforgery.SetCookieTokenAndHeader(context);
                    return Results.RedirectToRoute(DependenciesRoute);
                }

                await store.DistrustAsync(ecosystem, id, version, cancellationToken);
                return Results.RedirectToRoute(DependenciesRoute);
            })
            .WithName("DistrustDependencies")
            .WithMetadata(admin);

        builder.MapPost(
            "/dependencies/distrust-all",
            async (
                ITrustStore store,
                HttpContext context,
                IAntiforgery antiforgery,
                CancellationToken cancellationToken) =>
            {
                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    antiforgery.SetCookieTokenAndHeader(context);
                    return Results.RedirectToRoute(DependenciesRoute);
                }

                await store.DistrustAllAsync(cancellationToken);
                return Results.RedirectToRoute(DependenciesRoute);
            })
            .WithName("DistrustAllDependencies")
            .WithMetadata(admin);

        builder.MapGet("/github-webhook", (IOptions<GitHubOptions> options) => Results.Extensions.RazorSlice<Debug, GitHubOptions>(options.Value))
               .WithMetadata(admin);

        return builder;
    }

    private static async Task<(IReadOnlyList<WebhookDelivery> Deliveries, string? Cursor)> GetDeliveries(
        IGitHubClientForApp client,
        string? cursor = null)
    {
        // See https://docs.github.com/en/rest/apps/webhooks#list-deliveries-for-an-app-webhook
        var uri = new Uri("app/hook/deliveries", UriKind.Relative);

        var parameters = new Dictionary<string, string>(2)
        {
            ["per_page"] = "100",
        };

        if (cursor is not null)
        {
            parameters["cursor"] = cursor;
        }

        try
        {
            var response = await client.Connection.Get<List<WebhookDelivery>>(
                uri,
                parameters,
                "application/vnd.github+json");

            if (response.HttpResponse.StatusCode is not HttpStatusCode.OK)
            {
                return ([], null);
            }

            var deliveries = response.Body;
            var next = ExtractCursor(response.HttpResponse.Headers);

            return (deliveries, next);
        }
        catch (NotFoundException)
        {
            return ([], null);
        }
    }

    private static string? ExtractCursor(IReadOnlyDictionary<string, string> headers)
    {
        string? cursor = null;

        if (headers.TryGetValue("Link", out var link))
        {
            var nextUrl = link
                .Split(',')
                .FirstOrDefault((p) => p.EndsWith("; rel=\"next\"", StringComparison.Ordinal));

            if (nextUrl is not null)
            {
                var url = nextUrl.Split(';')[0];
                url = url.Trim('<', '>');

                var query = QueryHelpers.ParseQuery(url);

                if (query.TryGetValue("cursor", out var value))
                {
                    cursor = value.ToString();
                }
            }
        }

        return cursor;
    }

    private sealed class SetAntiforgeryCookieFilter : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            antiforgery.SetCookieTokenAndHeader(context.HttpContext);

            return await next(context);
        }
    }
}
