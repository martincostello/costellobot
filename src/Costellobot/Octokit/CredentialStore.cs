// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace Octokit;

public abstract class CredentialStore : ICredentialStore
{
    protected static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);
    protected static readonly TimeSpan TokenSkew = TimeSpan.FromMinutes(1);

    protected static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TokenLifetime - TokenSkew };
    protected static readonly string[] CacheTags = ["all", "github"];

    public abstract Task<Credentials> GetCredentials();
}
