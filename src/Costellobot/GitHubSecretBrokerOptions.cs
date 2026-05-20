// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class GitHubSecretBrokerOptions
{
    public Dictionary<string, Dictionary<string, Dictionary<string, GitHubTokenProfileOptions>>> Repositories { get; set; } = [];

    public IList<string> Tokens { get; set; } = [];

    public Uri VaultUri { get; set; } = default!;
}
