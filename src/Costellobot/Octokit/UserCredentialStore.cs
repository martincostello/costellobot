// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot;
using Microsoft.Extensions.Options;

namespace Octokit;

public sealed class UserCredentialStore(IOptionsMonitor<GitHubOptions> options) : ICredentialStore
{
    public Task<Credentials> GetCredentials()
    {
        var credentials = Credentials.Anonymous;

        if (options.CurrentValue.AccessToken is { Length: > 0 } token)
        {
            credentials = new(token);
        }

        return Task.FromResult(credentials);
    }
}
