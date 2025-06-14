// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;

namespace MartinCostello.Costellobot.Registries;

public sealed partial class DockerPackageRegistry(
    HttpClient client,
    HybridCache cache) : PackageRegistry(client)
{
    internal const string MicrosoftArtifactRegistry = "mcr.microsoft.com";

    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(1) };
    private static readonly string[] CacheTags = ["all", "mar"];

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.Docker;

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version)
    {
        var isMicrosoftImage = await cache.GetOrCreateAsync(
            $"mar:{id}",
            (Client, id),
            static async (context, _) => await IsImageFromMicrosoftArtifactRegistryAsync(context.Client, context.id),
            CacheEntryOptions,
            CacheTags);

        if (isMicrosoftImage)
        {
            return [MicrosoftArtifactRegistry];
        }

        var parts = id.Split('/');
        return parts.Length > 0 ? [parts[0]] : [];
    }

    private static async Task<bool> IsImageFromMicrosoftArtifactRegistryAsync(HttpClient client, string id)
    {
        var uri = new Uri($"api/v1/catalog/{id}/details?reg=mar", UriKind.Relative);

        try
        {
            var entry = await client.GetFromJsonAsync(uri, MicrosoftArtifactRegistryJsonSerializerContext.Default.CatalogEntry);
            return entry?.Publisher is "Microsoft";
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private sealed class CatalogEntry
    {
        [JsonPropertyName("publisher")]
        public string Publisher { get; set; } = string.Empty;
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(CatalogEntry))]
    private sealed partial class MicrosoftArtifactRegistryJsonSerializerContext : JsonSerializerContext;
}
