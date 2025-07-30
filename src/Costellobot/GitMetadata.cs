// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Reflection;

namespace MartinCostello.Costellobot;

public static class GitMetadata
{
    public static string BuildId { get; } = GetMetadataValue("BuildId", string.Empty);

    public static string Branch { get; } = GetMetadataValue("CommitBranch", "Unknown");

    public static string Commit { get; } = GetMetadataValue("CommitHash", "HEAD");

    public static string RepositoryUrl { get; } = GetRepositoryUrl();

    public static DateTimeOffset Timestamp { get; } = DateTimeOffset.Parse(GetMetadataValue("BuildTimestamp", DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    public static string Version { get; } = typeof(GitMetadata).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    public static string BranchUrl => $"{RepositoryUrl}/tree/{Branch}";

    public static string BuildUrl => $"{RepositoryUrl}/actions/runs/{BuildId}";

    public static string CommitUrl => $"{RepositoryUrl}/commit/{Commit}";

    public static string RepositoryName
    {
        get
        {
            var url = GetRepositoryUrl();
            var uri = new Uri(url);
            return uri.Segments.DefaultIfEmpty("costellobot").ElementAtOrDefault(2)!.TrimEnd('/');
        }
    }

    public static string RepositoryOwner
    {
        get
        {
            var url = GetRepositoryUrl();
            var uri = new Uri(url);
            return uri.Segments.DefaultIfEmpty("martincostello").ElementAtOrDefault(1)!.TrimEnd('/');
        }
    }

    private static string GetMetadataValue(string name, string defaultValue)
    {
        return typeof(GitMetadata).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where((p) => string.Equals(p.Key, name, StringComparison.Ordinal))
            .Select((p) => p.Value)
            .FirstOrDefault() ?? defaultValue;
    }

    private static string GetRepositoryUrl()
    {
        string repository = GetMetadataValue("RepositoryUrl", "https://github.com/martincostello/costellobot");

        const string Suffix = ".git";
        if (repository.EndsWith(Suffix, StringComparison.Ordinal))
        {
            repository = repository[..^Suffix.Length];
        }

        return repository;
    }
}
