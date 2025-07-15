// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class RepositoryDispatchHandler(
    HybridCache cache,
    HttpClient client,
    ILogger<RepositoryDispatchHandler> logger) : IHandler
{
    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(1) };
    private static readonly string[] CacheTags = ["all", "annotations"];

    public async Task HandleAsync(WebhookEvent message, CancellationToken cancellationToken)
    {
        if (message is RepositoryDispatchEvent body && body.ClientPayload is JsonElement payload)
        {
            switch (body.Action)
            {
                case WellKnownGitHubEvents.RepositoryDispatchActionValue.DeploymentStarted:
                    await CreateAnnotationAsync(payload, cancellationToken);
                    break;

                case WellKnownGitHubEvents.RepositoryDispatchActionValue.DeploymentCompleted:
                    await UpdateAnnotationAsync(payload, cancellationToken);
                    break;

                default:
                    break;
            }
        }
    }

    private static string AnnotationCacheKey(string repository, string runNumber, string runAttempt)
        => $"annotation:{repository}:{runNumber}:{runAttempt}";

    private static long GetInt64(JsonElement element, string propertyName)
        => element.GetProperty(propertyName).GetInt64();

    private static string GetString(JsonElement element, string propertyName)
        => element.GetProperty(propertyName).GetString()!;

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()!
            : null;
    }

    private async Task CreateAnnotationAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var context = GrafanaJsonSerializerContext.Default;

        string application = GetString(payload, "application");
        string repository = GetString(payload, "repository");
        string runAttempt = GetString(payload, "runAttempt");
        string runId = GetString(payload, "runId");
        string runNumber = GetString(payload, "runNumber");
        string serverUrl = GetString(payload, "serverUrl");
        string sha = GetString(payload, "sha");
        long timestamp = GetInt64(payload, "timestamp");

        string? environment = GetOptionalString(payload, "environment");
        string? @namespace = GetOptionalString(payload, "namespace");

        string commitSha = sha[0..7];
        string commitUrl = $"{serverUrl}/{repository}/commit/{sha}";
        string workflowUrl = $"{serverUrl}/{repository}/actions/runs/{runId}";

        string text = $@"Deployed <a href=""{workflowUrl}"">#{runNumber}:{runAttempt}</a> with commit <a href=""{commitUrl}"">{commitSha}</a>";

        var tags = new List<string>
        {
            "deployment",
            $"service.name={application}",
        };

        if (!string.IsNullOrWhiteSpace(environment))
        {
            tags.Add($"deployment.environment={environment}");
        }

        if (!string.IsNullOrWhiteSpace(@namespace))
        {
            tags.Add($"service.namespace={@namespace}");
        }

        var request = new CreateAnnotationRequest()
        {
            Tags = tags,
            Text = text,
            Time = timestamp,
        };

        using var response = await client.PostAsJsonAsync(
            "api/annotations",
            request,
            context.CreateAnnotationRequest,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var annotation = await response.Content.ReadFromJsonAsync(
                context.CreateAnnotationResponse,
                cancellationToken);

            Log.CreatedAnnotation(logger, annotation!.Id);

            await cache.SetAsync(
                AnnotationCacheKey(repository, runNumber, runAttempt),
                annotation.Id,
                CacheEntryOptions,
                CacheTags,
                cancellationToken);
        }
        else
        {
            Log.CreateAnnotationFailed(logger, response.StatusCode);
        }
    }

    private async Task UpdateAnnotationAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        string repository = GetString(payload, "repository");
        string runAttempt = GetString(payload, "runAttempt");
        string runNumber = GetString(payload, "runNumber");
        long timestamp = GetInt64(payload, "timestamp");

        var id = await cache.GetOrCreateAsync<long>(
            AnnotationCacheKey(repository, runNumber, runAttempt),
            (_) => ValueTask.FromResult(-1L),
            cancellationToken: cancellationToken);

        if (id is not -1)
        {
            using var response = await client.PatchAsJsonAsync(
                $"api/annotations/{id}",
                new() { TimeEnd = timestamp },
                GrafanaJsonSerializerContext.Default.UpdateAnnotationRequest,
                cancellationToken);

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
