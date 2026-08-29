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
        if (Cache.Count > MaximumSize)
        {
            Cache.Clear();
        }

        options |= RegexOptions.Compiled;

        return Cache.GetOrAdd(
            (pattern, options, timeout),
            static (key) => new Regex(key.Pattern, key.Options, key.Timeout));
    }
}
