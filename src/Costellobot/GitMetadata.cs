// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Reflection;

namespace MartinCostello.Costellobot;

public static class GitMetadata
{
    public static string BuildId { get; } = GetMetadataValue("BuildId", string.Empty);

    public static string Branch { get; } = GetMetadataValue("CommitBranch", "Unknown");

    public static string Commit { get; } = GetMetadataValue("CommitHash", "HEAD");

    public static DateTimeOffset Timestamp { get; } = DateTimeOffset.Parse(GetMetadataValue("BuildTimestamp", DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    private static string GetMetadataValue(string name, string defaultValue)
    {
        return typeof(GitMetadata).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where((p) => string.Equals(p.Key, name, StringComparison.Ordinal))
            .Select((p) => p.Value)
            .FirstOrDefault() ?? defaultValue;
    }
}
