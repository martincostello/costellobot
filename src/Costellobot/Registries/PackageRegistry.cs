// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Registries;

public abstract class PackageRegistry(HttpClient client) : IPackageRegistry
{
    public abstract DependencyEcosystem Ecosystem { get; }

    protected HttpClient Client { get; } = client;

    public abstract Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        string owner,
        string repository,
        string id,
        string version);
}
