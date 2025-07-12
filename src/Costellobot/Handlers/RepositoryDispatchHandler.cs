// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class RepositoryDispatchHandler(
    HybridCache cache,
    HttpClient client,
    IOptionsMonitor<GrafanaOptions> options,
    ILogger<RepositoryDispatchHandler> logger) : IHandler
{
    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(1) };
    private static readonly string[] CacheTags = ["all", "annotations"];

    public async Task HandleAsync(WebhookEvent message)
    {
        if (message is not RepositoryDispatchEvent body || body.ClientPayload is not { } payload)
        {
            return;
        }

        var grafana = options.CurrentValue;

        client.BaseAddress = new(grafana.Url, UriKind.Absolute);
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", grafana.Token);

        var context = GrafanaJsonSerializerContext.Default;

        if (body.Action is "deployment_started")
        {
            string environment = payload.environment;
            string repository = payload.repository;
            string runAttempt = payload.runAttempt;
            string runId = payload.runId;
            string runNumber = payload.runNumber;
            string serverUrl = payload.serverUrl;
            string service = payload.service;
            string sha = payload.sha;
            long timestamp = payload.timestamp;

            string commitSha = sha[0..7];
            string commitUrl = $"{serverUrl}/{repository}/commit/{sha}";
            string workflowUrl = $"{serverUrl}/{repository}/actions/runs/{runId}";

            string text = $@"Deployed <a href=""${workflowUrl}"">#{runNumber}:{runAttempt}</a> with commit <a href=""${commitUrl}"">${commitSha}</a>";

            var request = new CreateAnnotationRequest()
            {
                Tags =
                [
                    "deployment",
                    $"environment:{environment}",
                    $"service:{service}",
                ],
                Text = text,
                Time = timestamp,
            };

            using var response = await client.PostAsJsonAsync("/api/annotations", request, context.CreateAnnotationRequest);

            if (response.IsSuccessStatusCode)
            {
                var annotation = await response.Content.ReadFromJsonAsync(context.CreateAnnotationResponse);

                Log.CreatedAnnotation(logger, annotation!.Id);

                await cache.SetAsync(
                    CacheKey(repository, runNumber, runAttempt),
                    annotation.Id,
                    CacheEntryOptions,
                    CacheTags);
            }
            else
            {
                Log.CreateAnnotationFailed(logger, response.StatusCode);
            }
        }
        else if (body.Action is "deployment_completed")
        {
            string repository = payload.repository;
            string runAttempt = payload.runAttempt;
            string runNumber = payload.runNumber;
            long timestamp = payload.timestamp;

            var id = await cache.GetOrCreateAsync<long>(
                CacheKey(repository, runNumber, runAttempt),
                (_) => ValueTask.FromResult(-1L));

            if (id is not -1)
            {
                using var response = await client.PatchAsJsonAsync(
                    $"/api/annotations/{id}",
                    new() { TimeEnd = timestamp },
                    GrafanaJsonSerializerContext.Default.UpdateAnnotationRequest);

                if (response.IsSuccessStatusCode)
                {
                    Log.UpdatedAnnotation(logger, id);
                }
                else
                {
                    Log.UpdateAnnotationFailed(logger, id, response.StatusCode);
                }
            }
        }

        static string CacheKey(string repository, string runNumber, string runAttempt)
            => $"annotation:{repository}:{runNumber}:{runAttempt}";
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Created annotation {Id}.")]
        public static partial void CreatedAnnotation(ILogger logger, long id);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "Creating annotation failed with HTTP status code {StatusCode}.")]
        public static partial void CreateAnnotationFailed(ILogger logger, HttpStatusCode statusCode);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Updated annotation {Id}.")]
        public static partial void UpdatedAnnotation(ILogger logger, long id);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "Updating annotation {Id} failed with HTTP status code {StatusCode}.")]
        public static partial void UpdateAnnotationFailed(ILogger logger, long id, HttpStatusCode statusCode);
    }

    private sealed class CreateAnnotationRequest
    {
        [JsonPropertyName("tags")]
        public required IList<string> Tags { get; init; }

        [JsonPropertyName("text")]
        public required string Text { get; init; }

        [JsonPropertyName("time")]
        public required long Time { get; init; }
    }

    private sealed class CreateAnnotationResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }

    private sealed class UpdateAnnotationRequest
    {
        [JsonPropertyName("timeEnd")]
        public required long TimeEnd { get; init; }
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(CreateAnnotationRequest))]
    [JsonSerializable(typeof(CreateAnnotationResponse))]
    [JsonSerializable(typeof(UpdateAnnotationRequest))]
    private sealed partial class GrafanaJsonSerializerContext : JsonSerializerContext;
}
