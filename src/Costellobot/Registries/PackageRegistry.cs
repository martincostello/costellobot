// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Registries;

public abstract class PackageRegistry : IPackageRegistry
{
    protected PackageRegistry(HttpClient client)
    {
        Client = client;
    }

    public abstract DependencyEcosystem Ecosystem { get; }

    protected HttpClient Client { get; }

    public virtual Task<bool> AreOwnersTrustedAsync(IReadOnlyList<string> owners)
        => Task.FromResult(false);

    public abstract Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        string owner,
        string repository,
        string id,
        string version);
}
