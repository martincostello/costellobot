// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MartinCostello.Costellobot;

/// <summary>
/// A helper that caches compiled <see cref="Regex"/> instances for patterns that are not known at compile-time.
/// </summary>
internal static class RegexCache
{
    /// <summary>
    /// The maximum number of entries to retain in the cache.
    /// </summary>
    private const int MaximumSize = 512;

    private static readonly ConcurrentDictionary<(string Pattern, RegexOptions Options, TimeSpan Timeout), Regex> Cache = new();

    /// <summary>
    /// Gets a compiled <see cref="Regex"/> for the specified pattern, options and timeout, reusing a
    /// previously created instance for the same combination if one is already cached.
    /// </summary>
    /// <param name="pattern">The regular expression pattern.</param>
    /// <param name="options">The <see cref="RegexOptions"/> to use to create the <see cref="Regex"/>.</param>
    /// <param name="timeout">The timeout to use for the <see cref="Regex"/>.</param>
    /// <returns>
    /// The cached (or newly created) <see cref="Regex"/> instance for the specified pattern.
    /// </returns>
    public static Regex GetOrAdd(string pattern, RegexOptions options, TimeSpan timeout)
    {
        options |= RegexOptions.Compiled;

        var key = (pattern, options, timeout);

        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        TrimIfOverCapacity();

        return Cache.GetOrAdd(
            key,
            static (k) => new Regex(k.Pattern, k.Options, k.Timeout));
    }

    private static void TrimIfOverCapacity()
    {
        int excess = Cache.Count - MaximumSize;

        if (excess <= 0)
        {
            return;
        }

        int toRemove = excess + (MaximumSize / 2);

        foreach (var key in Cache.Keys)
        {
            if (toRemove <= 0)
            {
                break;
            }

            if (Cache.TryRemove(key, out _))
            {
                toRemove--;
            }
        }
    }
}
