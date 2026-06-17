// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Models;
using NuGet.Versioning;

namespace MartinCostello.Costellobot;

/// <summary>
/// A class that orders dependencies for display and marks the versions that are
/// superseded by a more recent version of the same dependency as being outdated.
/// </summary>
public static class DependencyHighlighter
{
    private static readonly IComparer<string> VersionComparer = Comparer<string>.Create(Compare);

    /// <summary>
    /// Orders the specified dependencies for display and determines which are outdated.
    /// </summary>
    /// <typeparam name="T">The type of the dependency.</typeparam>
    /// <param name="ecosystem">The ecosystem the dependencies belong to.</param>
    /// <param name="dependencies">The dependencies to order.</param>
    /// <param name="timestamp">A delegate to get the timestamp associated with a dependency.</param>
    /// <returns>
    /// The dependencies ordered for display, each paired with whether it is outdated.
    /// </returns>
    public static IReadOnlyList<OrderedDependency<T>> Order<T>(
        DependencyEcosystem ecosystem,
        IEnumerable<T> dependencies,
        Func<T, DateTimeOffset?> timestamp)
        where T : IDependency
    {
        var ordered = new List<OrderedDependency<T>>();

        foreach (var group in dependencies.GroupBy((p) => p.Id, StringComparer.Ordinal))
        {
            ordered.AddRange(MarkOutdated(ecosystem, [.. group], timestamp));
        }

        return
        [
            .. ordered
                .OrderBy((p) => p.Dependency.Id, StringComparer.Ordinal)
                .ThenBy((p) => p.IsOutdated)
                .ThenByDescending((p) => p.Dependency.Version, VersionComparer),
        ];
    }

    private static IEnumerable<OrderedDependency<T>> MarkOutdated<T>(
        DependencyEcosystem ecosystem,
        IReadOnlyList<T> group,
        Func<T, DateTimeOffset?> timestamp)
        where T : IDependency
    {
        if (ecosystem is DependencyEcosystem.Docker)
        {
            return MarkDockerOutdated(group, timestamp);
        }
        else if (ecosystem is DependencyEcosystem.GitHubActions)
        {
            return MarkGitHubActionsOutdated(group);
        }
        else
        {
            return MarkOutdatedByVersion(group);
        }
    }

    private static IEnumerable<OrderedDependency<T>> MarkOutdatedByVersion<T>(IReadOnlyList<T> group)
        where T : IDependency
    {
        var latest = group.Select((p) => p.Version).Max(VersionComparer)!;
        return group.Select((p) => new OrderedDependency<T>(p, Compare(p.Version, latest) < 0));
    }

    private static IEnumerable<OrderedDependency<T>> MarkDockerOutdated<T>(
        IReadOnlyList<T> group,
        Func<T, DateTimeOffset?> timestamp)
        where T : IDependency
    {
        // A Docker version may be pinned to a specific image with a digest of the
        // form "{tag}-{digest}" (e.g. "8.8.0-0a972..."). Such a version is treated
        // as equivalent to the plain "{tag}" version for the purposes of determining
        // which version is the most recent, so that the digest does not cause an
        // otherwise current version to be highlighted as outdated.
        var entries = group
            .Select((p) => (Dependency: p, BaseVersion: DockerBaseVersion(p.Version), HasDigest: HasDockerDigest(p.Version)))
            .ToArray();

        var latestBaseVersion = entries.Select((p) => p.BaseVersion).Max(VersionComparer)!;

        // When there are multiple digests for the latest version, the one with the
        // newest timestamp is considered the most recent and the others are outdated.
        var latestDigest = entries
            .Where((p) => p.HasDigest && Compare(p.BaseVersion, latestBaseVersion) is 0)
            .OrderByDescending((p) => timestamp(p.Dependency) ?? DateTimeOffset.MinValue)
            .ThenByDescending((p) => p.Dependency.Version, VersionComparer)
            .Select((p) => p.Dependency)
            .FirstOrDefault();

        return entries.Select((entry) =>
        {
            bool isLatestVersion = Compare(entry.BaseVersion, latestBaseVersion) is 0;

            bool isOutdated = (isLatestVersion, entry.HasDigest) switch
            {
                (false, _) => true,
                (true, false) => false,
                (true, true) => !ReferenceEquals(entry.Dependency, latestDigest),
            };

            return new OrderedDependency<T>(entry.Dependency, isOutdated);
        });
    }

    private static IEnumerable<OrderedDependency<T>> MarkGitHubActionsOutdated<T>(IReadOnlyList<T> group)
        where T : IDependency
    {
        var latest = group.Select((p) => p.Version).Max(VersionComparer)!;
        int? latestMajor = NuGetVersion.TryParse(latest, out var version) ? version.Major : null;

        return group.Select((p) =>
        {
            // A major-version-only entry (e.g. "23") is treated as equivalent to a more
            // specific version that shares the same major version (e.g. "23.2.0"), so that
            // it is not highlighted as outdated when a more specific version is the latest.
            bool isOutdated =
                Compare(p.Version, latest) is not 0 &&
                !(latestMajor is { } major && IsMajorOnly(p.Version, out int itemMajor) && itemMajor == major);

            return new OrderedDependency<T>(p, isOutdated);
        });
    }

    private static bool HasDockerDigest(string version)
        => TrySplitDockerDigest(version, out _);

    private static string DockerBaseVersion(string version)
        => TrySplitDockerDigest(version, out var baseVersion) ? baseVersion : version;

    private static bool TrySplitDockerDigest(string version, out string baseVersion)
    {
        // A Docker digest is the SHA-256 hash of the image (i.e. 64 hexadecimal characters).
        int index = version.LastIndexOf('-', StringComparison.Ordinal);

        if (index > 0 && IsDigest(version.AsSpan(index + 1)))
        {
            baseVersion = version[..index];
            return true;
        }

        baseVersion = version;
        return false;

        static bool IsDigest(ReadOnlySpan<char> value)
        {
            if (value.Length is not 64)
            {
                return false;
            }

            foreach (char ch in value)
            {
                if (!char.IsAsciiHexDigit(ch))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static bool IsMajorOnly(string version, out int major)
    {
        major = 0;
        return !version.Contains('.', StringComparison.Ordinal) &&
               int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out major);
    }

    private static int Compare(string x, string y)
    {
        if (NuGetVersion.TryParse(x, out var versionX) &&
            NuGetVersion.TryParse(y, out var versionY))
        {
            return versionX.CompareTo(versionY);
        }

        return string.Compare(x, y, StringComparison.Ordinal);
    }
}
