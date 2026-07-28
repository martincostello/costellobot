// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;

namespace MartinCostello.Costellobot.Registries;

public sealed partial class GoPackageRegistry(
    HttpClient client,
    HybridCache cache) : PackageRegistry(client)
{
    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(1) };
    private static readonly string[] CacheTags = ["all", "go-pkg"];

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.GoModules;

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        var normalizedVersion = $"v{version}";
        var escapedVersion = Uri.EscapeDataString(normalizedVersion);

        // https://go.dev/blog/pkgsite-api
        var uri = new Uri($"v1beta/package/{id}?version={escapedVersion}", UriKind.Relative);

        Module? module = await cache.GetOrCreateAsync(
            $"go-pkg:{id}@{normalizedVersion}",
            (Client, uri),
            static async (context, token) =>
            {
                try
                {
                    return await context.Client.GetFromJsonAsync(context.uri, GoPackageJsonSerializerContext.Default.Module, token);
                }
                catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
                {
                    return null;
                }
            },
            CacheEntryOptions,
            CacheTags,
            cancellationToken);

        // If the module's path is in GitHub, the owner is the first
        // two parts of the path (e.g. "github.com/martincostello").
        if (module is { } &&
            string.Equals(normalizedVersion, module.Version, StringComparison.Ordinal) &&
            module.ModulePath is { } modulePath &&
            modulePath.StartsWith("github.com/", StringComparison.Ordinal))
        {
            var parts = modulePath.Split('/', StringSplitOptions.None);
            if (parts.Length > 1)
            {
                return [string.Join('/', parts.Take(2))];
            }
        }

        return [];
    }

    private sealed class Module
    {
        [JsonPropertyName("modulePath")]
        public string ModulePath { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(Module))]
    private sealed partial class GoPackageJsonSerializerContext : JsonSerializerContext;
}
