// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Hybrid;

namespace MartinCostello.Costellobot.Registries;

public sealed partial class NuGetPackageRegistry : PackageRegistry
{
    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(1) };
    private static readonly string[] CacheTags = ["all", "nuget"];

    private readonly HybridCache _cache;
    private readonly string _cacheKeyPrefix;
    private readonly ILogger _logger;
    private readonly bool _private;
    private readonly string[] _registryOwners;

    public NuGetPackageRegistry(
        HttpClient client,
        HybridCache cache,
        ILogger<NuGetPackageRegistry> logger)
        : base(client)
    {
        _cache = cache;
        _cacheKeyPrefix = client.BaseAddress?.ToString() ?? string.Empty;
        _logger = logger;
        _private = false;
        _registryOwners = [];

        if (client.BaseAddress?.Host is "f.feedz.io")
        {
            // Example: https://f.feedz.io/martincostello/costellobot/nuget/
            var segments = client.BaseAddress.Segments;
            var owner = segments.Length switch
            {
                4 when string.Equals(segments[3], "nuget/", StringComparison.Ordinal) => segments[1].TrimEnd('/'),
                _ => string.Empty,
            };

            if (!string.IsNullOrEmpty(owner))
            {
                _private = true;
                _registryOwners = [owner];
            }
        }
        else if (client.BaseAddress?.Host is "www.myget.org")
        {
            // Example https://www.myget.org/F/opentelemetry/api/v3/
            var segments = client.BaseAddress.Segments;
            var owner = segments.Length switch
            {
                5 when string.Equals(segments[1], "F/", StringComparison.Ordinal) => segments[2].TrimEnd('/'),
                _ => string.Empty,
            };

            if (!string.IsNullOrEmpty(owner))
            {
                _private = true;
                _registryOwners = [owner];
            }
        }
    }

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.NuGet;

    public override Task<bool> AreOwnersTrustedAsync(IReadOnlyList<string> owners, CancellationToken cancellationToken)
    {
        bool trusted = false;

        if (_private && owners.Count > 0)
        {
            trusted = owners.SequenceEqual(_registryOwners, StringComparer.Ordinal);
        }

        return Task.FromResult(trusted);
    }

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        if (!NuGet.Versioning.NuGetVersion.TryParse(version, out var _))
        {
            Log.InvalidNuGetPackageVersion(_logger, version);
            return [];
        }

        // https://docs.microsoft.com/nuget/api/search-query-service-resource#versioning
        // NuGet.org has multiple entries for fallback, so we could extend to support that in future
        var baseAddress = await GetBaseAddressAsync("SearchQueryService/3.5.0", cancellationToken);

        // Fallback resource for other NuGet implementations such as feedz.io and MyGet.org
        baseAddress ??= await GetBaseAddressAsync("SearchQueryService", cancellationToken);

        if (baseAddress is null)
        {
            return [];
        }

        // https://docs.microsoft.com/nuget/api/search-query-service-resource#search-for-packages
        var query = new Dictionary<string, string?>()
        {
            ["prerelease"] = "true",
            ["q"] = $"PackageId:{id}",
            ["semVerLevel"] = "2.0.0",
            ["take"] = "1",
        };

        var uri = QueryHelpers.AddQueryString(baseAddress, query);
        var response = await Client.GetFromJsonAsync(uri, NuGetJsonSerializerContext.Default.SearchResponse, cancellationToken);

        if (response is null || response.Data is not { Count: > 0 } data)
        {
            return [];
        }

        var package = response.Data.FirstOrDefault((p) => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

        if (package is null ||
            !string.Equals(package.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var versionFound = package.Versions
            .Where((p) => string.Equals(p.Version, version, StringComparison.OrdinalIgnoreCase))
            .Select((p) => p.Version)
            .FirstOrDefault();

        versionFound ??= package.Versions
            .Where((p) => p.Version.StartsWith(version, StringComparison.Ordinal))
            .Where((p) => p.Version.Contains('+', StringComparison.Ordinal))
            .Select((p) => p.Version.Split('+')[0])
            .SingleOrDefault();

        versionFound ??= package.Version;

        if (!string.Equals(versionFound, version, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        // https://docs.microsoft.com/nuget/api/search-query-service-resource#search-result
        return package.Owners.ValueKind switch
        {
            JsonValueKind.Array => GetPackageOwners(package.Owners),
            JsonValueKind.String => GetPackageOwner(package.Owners),
            _ => _private ? _registryOwners : [],
        };

        static IReadOnlyList<string> GetPackageOwner(JsonElement owner)
        {
            var name = owner.GetString();
            return [name!];
        }

        static IReadOnlyList<string> GetPackageOwners(JsonElement owners)
            => [.. owners.EnumerateArray().Select((p) => p.GetString()!).Order()];
    }

    private async Task<string?> GetBaseAddressAsync(string type, CancellationToken cancellationToken)
    {
        var index = await GetServiceIndexAsync(cancellationToken);

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

    private async Task<ServiceIndex?> GetServiceIndexAsync(CancellationToken cancellationToken) =>
        await _cache.GetOrCreateAsync(
            $"{_cacheKeyPrefix}:nuget-service-index",
            Client,
            static async (client, token) => await client.GetFromJsonAsync("index.json", NuGetJsonSerializerContext.Default.ServiceIndex, cancellationToken: token),
            CacheEntryOptions,
            CacheTags,
            cancellationToken);

    [ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "The version string \"{Version}\" is not a valid NuGet package version.")]
        public static partial void InvalidNuGetPackageVersion(ILogger logger, string version);
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
        public IList<SearchResult> Data { get; set; } = [];
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
        public IList<PackageVersion> Versions { get; set; } = [];
    }

    private sealed class ServiceIndex
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("resources")]
        public IList<Resource> Resources { get; set; } = [];
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(SearchResponse))]
    [JsonSerializable(typeof(ServiceIndex))]
    private sealed partial class NuGetJsonSerializerContext : JsonSerializerContext;
}
