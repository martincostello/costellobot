// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;

namespace MartinCostello.Costellobot.Registries;

public sealed partial class NpmPackageRegistry(
    HttpClient client,
    HybridCache cache) : PackageRegistry(client)
{
    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(1) };
    private static readonly string[] CacheTags = ["all", "npm"];

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.Npm;

    public async Task<bool?> GetPackageAttestationAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        repository.ToString();

        Package? package = await GetPackageAsync(id, version, cancellationToken);

        if (package is null ||
            !string.Equals(package.Name, id, StringComparison.Ordinal) ||
            !string.Equals(package.Version, version, StringComparison.Ordinal))
        {
            return null;
        }

        return package.Distribution?.Attestations?.Provenance?.PredicateType == "https://slsa.dev/provenance/v1";
    }

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        Package? package = await GetPackageAsync(id, version, cancellationToken);

        if (package?.User?.Name is { } name &&
            !string.IsNullOrWhiteSpace(name) &&
            string.Equals(package.Name, id, StringComparison.Ordinal) &&
            string.Equals(package.Version, version, StringComparison.Ordinal))
        {
            return [name];
        }

        return [];
    }

    private async Task<Package?> GetPackageAsync(
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        var escapedId = Uri.EscapeDataString(id);
        var escapedVersion = Uri.EscapeDataString(version);

        // https://github.com/npm/registry/blob/main/docs/responses/package-metadata.md#package-metadata
        var uri = new Uri($"{escapedId}/{escapedVersion}", UriKind.Relative);

        return await cache.GetOrCreateAsync(
            $"{id}@{version}",
            async (token) =>
            {
                try
                {
                    return await Client.GetFromJsonAsync(uri, NpmJsonSerializerContext.Default.Package, token);
                }
                catch (HttpRequestException ex) when (IsNotFound(ex))
                {
                    return null;
                }
            },
            CacheEntryOptions,
            CacheTags,
            cancellationToken);

        static bool IsNotFound(HttpRequestException exception) =>
            exception.StatusCode switch
            {
                HttpStatusCode.NotFound => true,
                HttpStatusCode.MethodNotAllowed => true, // Returned if version is not x.y.z (e.g. 1.0)
                _ => false,
            };
    }

    private sealed class Package
    {
        [JsonPropertyName("dist")]
        public Distribution? Distribution { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("_npmUser")]
        public User? User { get; set; }
    }

    private sealed class Attestations
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("provenance")]
        public Provenance? Provenance { get; set; }
    }

    private sealed class Distribution
    {
        [JsonPropertyName("attestations")]
        public Attestations? Attestations { get; set; }
    }

    private sealed class Provenance
    {
        [JsonPropertyName("predicateType")]
        public string PredicateType { get; set; } = string.Empty;
    }

    private sealed class User
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(Package))]
    private sealed partial class NpmJsonSerializerContext : JsonSerializerContext;
}
