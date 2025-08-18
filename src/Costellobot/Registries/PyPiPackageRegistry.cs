// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;

namespace MartinCostello.Costellobot.Registries;

public sealed partial class PyPiPackageRegistry(
    HttpClient client,
    HybridCache cache) : PackageRegistry(client)
{
    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(1) };
    private static readonly string[] CacheTags = ["all", "pip"];

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.Pip;

    public async Task<bool?> GetPackageAttestationAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var attestation = await GetAttestationCachedAsync(id, version, cancellationToken);

        if (attestation is not { AttestationBundles.Count: > 0 })
        {
            return null;
        }

        var project = await GetProjectCachedAsync(id, version, cancellationToken);

        // TODO Verify the keys for getting the GitHub repository URL
        if (project?.Info?.ProjectUrls.TryGetValue("GitHub: repo", out var repositoryUrl) is not true ||
            !Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var repositoryUri) ||
            repositoryUri.Host is not "github.com")
        {
            return false;
        }

        var slug = repositoryUri.AbsolutePath.Trim('/');

        return attestation.AttestationBundles.All((p) => p.Publisher?.Kind is "GitHub" && p.Publisher.Repository == slug);
    }

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        var project = await GetProjectCachedAsync(id, version, cancellationToken);

        if (project?.Info is { } info &&
            !string.IsNullOrWhiteSpace(info.Name) &&
            !string.IsNullOrWhiteSpace(info.Maintainer) &&
            string.Equals(info.Name, id, StringComparison.Ordinal) &&
            string.Equals(info.Version, version, StringComparison.Ordinal))
        {
            return [info.Maintainer];
        }

        return [];
    }

    private static async Task<Attestation?> GetAttestationAsync(string id, string version, HttpClient client, CancellationToken cancellationToken)
    {
        var escapedId = Uri.EscapeDataString(id);
        var escapedVersion = Uri.EscapeDataString(version);

        // https://docs.pypi.org/api/integrity/#get-provenance-for-file
        var uri = new Uri($"/integrity/{escapedId}/{escapedVersion}/{escapedId}-{escapedVersion}.tar.gz/provenance", UriKind.Relative);

        try
        {
            return await client.GetFromJsonAsync(uri, PyPiJsonSerializerContext.Default.Attestation, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static async Task<Project?> GetProjectAsync(string id, string version, HttpClient client, CancellationToken cancellationToken)
    {
        var escapedId = Uri.EscapeDataString(id);
        var escapedVersion = Uri.EscapeDataString(version);

        // https://docs.pypi.org/api/json/#get-a-release
        var uri = new Uri($"/pypi/{escapedId}/{escapedVersion}/json", UriKind.Relative);

        try
        {
            return await client.GetFromJsonAsync(uri, PyPiJsonSerializerContext.Default.Project, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<Attestation?> GetAttestationCachedAsync(string id, string version, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"pypi-attestation:{id}@{version}",
            (id, version, Client),
            static async (context, token) => await GetAttestationAsync(context.id, context.version, context.Client, token),
            CacheEntryOptions,
            CacheTags,
            cancellationToken);

    private async Task<Project?> GetProjectCachedAsync(string id, string version, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"pypi:{id}@{version}",
            (id, version, Client),
            static async (context, token) => await GetProjectAsync(context.id, context.version, context.Client, token),
            CacheEntryOptions,
            CacheTags,
            cancellationToken);

    private sealed class Attestation
    {
        [JsonPropertyName("attestation_bundles")]
        public IList<AttestationBundle> AttestationBundles { get; set; } = [];
    }

    private sealed class AttestationBundle
    {
        [JsonPropertyName("publisher")]
        public AttestationPublisher? Publisher { get; set; }
    }

    private sealed class AttestationPublisher
    {
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("repository")]
        public string? Repository { get; set; }
    }

    private sealed class Project
    {
        [JsonPropertyName("info")]
        public ProjectInfo Info { get; set; } = default!;
    }

    private sealed class ProjectInfo
    {
        [JsonPropertyName("maintainer")]
        public string Maintainer { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("project_urls")]
        public Dictionary<string, string> ProjectUrls { get; set; } = [];
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(Attestation))]
    [JsonSerializable(typeof(Project))]
    private sealed partial class PyPiJsonSerializerContext : JsonSerializerContext;
}
