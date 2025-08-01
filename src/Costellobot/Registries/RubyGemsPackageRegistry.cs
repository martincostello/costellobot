// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;

namespace MartinCostello.Costellobot.Registries;

public sealed partial class RubyGemsPackageRegistry(
    HttpClient client,
    HybridCache cache) : PackageRegistry(client)
{
    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(1) };
    private static readonly string[] CacheTags = ["all", "ruby-gems"];

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.Ruby;

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        var escapedId = Uri.EscapeDataString(id);

        // https://guides.rubygems.org/rubygems-org-api/#owner-methods
        var uri = new Uri($"api/v1/gems/{escapedId}/owners.json", UriKind.Relative);

        Owner[]? owners = await cache.GetOrCreateAsync(
            $"ruby-gems:{id}",
            (Client, uri),
            static async (context, token) =>
            {
                try
                {
                    return await context.Client.GetFromJsonAsync(context.uri, RubyGemsJsonSerializerContext.Default.OwnerArray, token);
                }
                catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
                {
                    return null;
                }
            },
            CacheEntryOptions,
            CacheTags,
            cancellationToken);

        if (owners?.Length > 0)
        {
            return [.. owners
                .Where((p) => !string.IsNullOrWhiteSpace(p.Handle))
                .OrderBy((p) => p.Id)
                .Select((p) => p.Handle)
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        return [];
    }

    private sealed class Owner
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("handle")]
        public string Handle { get; set; } = string.Empty;
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(Owner[]))]
    private sealed partial class RubyGemsJsonSerializerContext : JsonSerializerContext;
}
