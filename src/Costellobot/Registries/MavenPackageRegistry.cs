// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Hybrid;

namespace MartinCostello.Costellobot.Registries;

/// <summary>
/// A class representing the Maven Central package registry.
/// </summary>
/// <param name="client">The HTTP client to use to query Maven Central.</param>
/// <param name="cache">The cache to use for responses from Maven Central.</param>
public sealed partial class MavenPackageRegistry(
    HttpClient client,
    HybridCache cache) : PackageRegistry(client)
{
    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(1) };
    private static readonly string[] CacheTags = ["all", "maven"];

    public override DependencyEcosystem Ecosystem => DependencyEcosystem.Maven;

    public override async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        RepositoryId repository,
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        if (!TryParseCoordinates(id, out var groupId, out var artifactId) ||
            !IsValidVersion(version))
        {
            return [];
        }

        // https://maven.apache.org/repositories/layout.html
        var path = string.Join('/', groupId.Split('.').Select(Uri.EscapeDataString));
        var escapedArtifactId = Uri.EscapeDataString(artifactId);
        var escapedVersion = Uri.EscapeDataString(version);

        var uri = new Uri($"{path}/{escapedArtifactId}/{escapedVersion}/{escapedArtifactId}-{escapedVersion}.pom", UriKind.Relative);

        // Only consider the namespaces to be the owners if the version of
        // the artifact was actually published to the registry under them.
        bool exists = await cache.GetOrCreateAsync(
            $"maven:{groupId}:{artifactId}@{version}",
            (Client, uri),
            static async (context, token) =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, context.uri);
                using var response = await context.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                if (response.StatusCode is HttpStatusCode.NotFound)
                {
                    return false;
                }

                response.EnsureSuccessStatusCode();
                return true;
            },
            CacheEntryOptions,
            CacheTags,
            cancellationToken);

        return exists ? GetNamespaces(groupId) : [];
    }

    /// <summary>
    /// Gets the namespaces that own a group ID, from most to least specific.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>
    /// The group ID and each of its parent namespaces that has at least two segments.
    /// Single-segment namespaces, such as <c>org</c>, cannot be owned by a publisher.
    /// </returns>
    private static string[] GetNamespaces(string groupId)
    {
        var segments = groupId.Split('.');
        var namespaces = new List<string>(segments.Length);

        for (int count = segments.Length; count >= 2; count--)
        {
            namespaces.Add(string.Join('.', segments.Take(count)));
        }

        return [.. namespaces];
    }

    private static bool TryParseCoordinates(
        string id,
        out string groupId,
        out string artifactId)
    {
        groupId = string.Empty;
        artifactId = string.Empty;

        // Dependency names are in the form "groupId:artifactId". Any further
        // components, such as a packaging type or classifier, are not supported.
        var parts = id.Split(':');

        if (parts.Length is not 2 ||
            !GroupIdPattern().IsMatch(parts[0]) ||
            !IsValidName(parts[1]))
        {
            return false;
        }

        groupId = parts[0];
        artifactId = parts[1];
        return true;
    }

    private static bool IsValidVersion(string version)
        => IsValidName(version, allowPlus: true);

    private static bool IsValidName(string value, bool allowPlus = false)
    {
        // Avoid any value that could be used to alter the path of the request
        if (value.Length is 0 || value is "." or "..")
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' || (allowPlus && c is '+')))
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]+(\.[A-Za-z0-9_-]+)*$", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex GroupIdPattern();
}
