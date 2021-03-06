// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;

namespace MartinCostello.Costellobot.Registries;

public sealed class GitHubActionsPackageRegistry : IPackageRegistry
{
    private readonly IGitHubClient _client;

    public GitHubActionsPackageRegistry(IGitHubClientForInstallation client)
    {
        _client = client;
    }

    public DependencyEcosystem Ecosystem => DependencyEcosystem.GitHubActions;

    public async Task<IReadOnlyList<string>> GetPackageOwnersAsync(
        string owner,
        string repository,
        string id,
        string version)
    {
        string[] parts = id.Split('/');

        if (parts.Length == 2)
        {
            string actionOwner = parts[0];
            string actionName = parts[1];

            // GitHub Actions tags that are versions are usually prefixed with
            // a 'v' but the version extracted from the commit message will just
            // have the version number. Based on what we find, prefer searching
            // for a tag prefixed with 'v' before looking for the verbatim tag.
            bool hasVersionPrefix = version.StartsWith('v');

            string[] refs;

            if (hasVersionPrefix)
            {
                refs = new[] { version, version[1..] };
            }
            else
            {
                refs = new[] { $"v{version}", version };
            }

            foreach (var reference in refs)
            {
                if (await ExistsAsync(() => _client.Git.Reference.Get(actionOwner, actionName, $"tags/{reference}")))
                {
                    return new[] { actionOwner };
                }
            }

            // If we didn't find the tag(s), maybe it's a sha to a specific commit
            if (await ExistsAsync(() => _client.Git.Commit.Get(actionOwner, actionName, version)))
            {
                return new[] { actionOwner };
            }
        }

        return Array.Empty<string>();

        static async Task<bool> ExistsAsync<T>(Func<Task<T>> resource)
        {
            try
            {
                _ = await resource();
                return true;
            }
            catch (NotFoundException)
            {
                return false;
            }
        }
    }
}
