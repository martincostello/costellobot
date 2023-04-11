// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace MartinCostello.Costellobot.Registries;

public sealed class NuGetPackageRegistry : PackageRegistry
{
    private static readonly Uri ServiceIndexUri = new("https://api.nuget.org/v3/index.json");

    private readonly IMemoryCache _cache;

    public NuGetPackageRegistry(HttpClient client, IMemoryCache cache)
        : base(client)
    {
        _cache = cache;
    }

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.NuGet;

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        string owner,
        string repository,
        string id,
        string version)
    {
        // https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#versioning
        var baseAddress = await GetBaseAddressAsync("SearchQueryService/3.5.0");

        if (baseAddress is null)
        {
            return Array.Empty<string>();
        }

        // https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#search-for-packages
        var query = new Dictionary<string, string?>()
        {
            ["prerelease"] = "true",
            ["q"] = $"PackageId:{id}",
            ["semVerLevel"] = "2.0.0",
            ["take"] = "1",
        };

        var uri = QueryHelpers.AddQueryString(baseAddress, query);
        var response = await Client.GetFromJsonAsync<SearchResponse>(uri);

        if (response is null || response.Data is not { Count: > 0 } data)
        {
            return Array.Empty<string>();
        }

        var package = response.Data
            .Where((p) => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (package is null ||
            !string.Equals(package.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        var versionFound = package.Versions
            .Where((p) => string.Equals(p.Version, version, StringComparison.OrdinalIgnoreCase))
            .Select((p) => p.Version)
            .FirstOrDefault();

        if (versionFound is null)
        {
            versionFound = package.Versions
                .Where((p) => p.Version.StartsWith(version, StringComparison.Ordinal))
                .Where((p) => p.Version.Contains('+', StringComparison.Ordinal))
                .Select((p) => p.Version.Split('+')[0])
                .SingleOrDefault();
        }

        versionFound ??= package.Version;

        if (!string.Equals(versionFound, version, StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        // https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#search-result
        return package.Owners.ValueKind switch
        {
            JsonValueKind.Array => GetPackageOwners(package.Owners),
            JsonValueKind.String => GetPackageOwner(package.Owners),
            _ => Array.Empty<string>(),
        };

        static IReadOnlyList<string> GetPackageOwner(JsonElement owner)
        {
            var name = owner.GetString();
            return new[] { name! };
        }

        static IReadOnlyList<string> GetPackageOwners(JsonElement owners)
        {
            return owners
                .EnumerateArray()
                .Select((p) => p.GetString()!)
                .Order()
                .ToArray();
        }
    }

    private async Task<string?> GetBaseAddressAsync(string type)
    {
        var index = await GetServiceIndexAsync();

        var resource = index?.Resources?
            .Where((p) => string.Equals(p.Type, type, StringComparison.Ordinal))
            .FirstOrDefault();

        string? baseAddress = null;

        if (resource is not null && Uri.TryCreate(resource.Id, UriKind.Absolute, out var uri))
        {
            baseAddress = uri.ToString();
        }

        return baseAddress;
    }

    private async Task<ServiceIndex?> GetServiceIndexAsync()
    {
        return await _cache.GetOrCreateAsync("nuget-service-index", async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await Client.GetFromJsonAsync<ServiceIndex>(ServiceIndexUri);
        });
    }

    private sealed class PackageVersion
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    private sealed class Resource
    {
        [JsonPropertyName("@id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("@type")]
        public string Type { get; set; } = string.Empty;
    }

    private sealed class SearchResponse
    {
        [JsonPropertyName("data")]
        public IList<SearchResult> Data { get; set; } = new List<SearchResult>();
    }

    private sealed class SearchResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("owners")]
        public JsonElement Owners { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("versions")]
        public IList<PackageVersion> Versions { get; set; } = new List<PackageVersion>();
    }

    private sealed class ServiceIndex
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("resources")]
        public IList<Resource> Resources { get; set; } = new List<Resource>();
    }
}
