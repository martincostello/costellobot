// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json.Serialization;

namespace MartinCostello.Costellobot.Registries;

public sealed partial class RubyGemsPackageRegistry(HttpClient client) : PackageRegistry(client)
{
    public override DependencyEcosystem Ecosystem => DependencyEcosystem.Ruby;

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version)
    {
        var escapedId = Uri.EscapeDataString(id);

        // https://guides.rubygems.org/rubygems-org-api/#owner-methods
        var uri = new Uri($"api/v1/gems/{escapedId}/owners.json", UriKind.Relative);

        Owner[]? owners;

        try
        {
            owners = await Client.GetFromJsonAsync(uri, RubyGemsJsonSerializerContext.Default.OwnerArray);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            owners = null;
        }

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
