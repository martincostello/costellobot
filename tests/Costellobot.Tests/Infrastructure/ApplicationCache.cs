// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;

namespace MartinCostello.Costellobot.Infrastructure;

internal sealed class ApplicationCache : HybridCache, IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void Dispose() => _cache.Dispose();

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _cache.GetOrCreateAsync<T>(key, async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = options?.Expiration;
            return await factory(state, cancellationToken);
        });

        return result!;
    }

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (tag is "all")
        {
            _cache.Compact(percentage: 100);
        }

        return ValueTask.CompletedTask;
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var entryOptions = new MemoryCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = options?.Expiration,
        };

        _cache.Set(key, value, entryOptions);

        return ValueTask.CompletedTask;
    }
}
