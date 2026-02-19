// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;

namespace MartinCostello.Costellobot.Registries;

public sealed partial class PyPiPackageRegistry(
    HttpClient client,
    HybridCache cache) : PackageRegistry(client)
{
    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(1) };
    private static readonly string[] CacheTags = ["all", "pypi"];

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.PyPI;

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        var escapedId = Uri.EscapeDataString(id);
        var escapedVersion = Uri.EscapeDataString(version);

        // https://docs.pypi.org/api/json/#get-a-release
        var uri = new Uri($"/pypi/{escapedId}/{escapedVersion}/json", UriKind.Relative);

        Project? project = await cache.GetOrCreateAsync(
            $"pypi:{id}",
            (Client, uri),
            static async (context, token) =>
            {
                try
                {
                    return await context.Client.GetFromJsonAsync(context.uri, PyPiJsonSerializerContext.Default.Project, token);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
            },
            CacheEntryOptions,
            CacheTags,
            cancellationToken);

        var owners = new List<string>();

        if (project?.Info is { } info &&
            !string.IsNullOrWhiteSpace(info.Name) &&
            string.Equals(info.Name, id, StringComparison.Ordinal) &&
            string.Equals(info.Version, version, StringComparison.Ordinal) &&
            project.Ownership is { } ownership)
        {
            if (ownership.Roles is { Count: > 0 } roles)
            {
                owners = [.. roles
                    .Where((p) => string.Equals(p.Role, "Owner", StringComparison.OrdinalIgnoreCase))
                    .Select((p) => p.User)
                    .Where((p) => !string.IsNullOrWhiteSpace(p))];
            }

            if (!string.IsNullOrEmpty(ownership.Organization))
            {
                owners.Add(ownership.Organization);
            }
        }

        return owners;
    }

    private sealed class Project
    {
        [JsonPropertyName("info")]
        public ProjectInfo Info { get; set; } = default!;

        [JsonPropertyName("ownership")]
        public OwnershipInfo Ownership { get; set; } = default!;
    }

    private sealed class ProjectInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    private sealed class OwnershipInfo
    {
        [JsonPropertyName("roles")]
        public IList<OwnershipRole> Roles { get; set; } = [];

        [JsonPropertyName("organization")]
        public string Organization { get; set; } = string.Empty;
    }

    private sealed class OwnershipRole
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("user")]
        public string User { get; set; } = string.Empty;
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(Project))]
    private sealed partial class PyPiJsonSerializerContext : JsonSerializerContext;
}
