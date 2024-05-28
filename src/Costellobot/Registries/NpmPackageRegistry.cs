// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json.Serialization;

namespace MartinCostello.Costellobot.Registries;

public sealed partial class NpmPackageRegistry(HttpClient client) : PackageRegistry(client)
{
    public override DependencyEcosystem Ecosystem => DependencyEcosystem.Npm;

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        string owner,
        string repository,
        string id,
        string version)
    {
        var escapedId = Uri.EscapeDataString(id);
        var escapedVersion = Uri.EscapeDataString(version);

        // https://github.com/npm/registry/blob/main/docs/responses/package-metadata.md#package-metadata
        var uri = new Uri($"{escapedId}/{escapedVersion}", UriKind.Relative);

        Package? package;

        try
        {
            package = await Client.GetFromJsonAsync(uri, NpmJsonSerializerContext.Default.Package);
        }
        catch (HttpRequestException ex) when (IsNotFound(ex))
        {
            package = null;
        }

        if (package?.User?.Name is { } name &&
            !string.IsNullOrWhiteSpace(name) &&
            string.Equals(package.Name, id, StringComparison.Ordinal) &&
            string.Equals(package.Version, version, StringComparison.Ordinal))
        {
            return [name];
        }

        return [];

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
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("_npmUser")]
        public User? User { get; set; }
    }

    private sealed class User
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(Package))]
    private sealed partial class NpmJsonSerializerContext : JsonSerializerContext
    {
    }
}
