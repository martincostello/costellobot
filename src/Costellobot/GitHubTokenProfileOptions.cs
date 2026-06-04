// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class GitHubTokenProfileOptions
{
    public string? AppId { get; set; }

    public Dictionary<string, string> AppPermissions { get; set; } = [];

    public IList<string> Branches { get; set; } = [];

    public IList<string> Environments { get; set; } = [];

    public IList<string> Events { get; set; } = [];

    public IList<string> Tags { get; set; } = [];

    public IList<string>? TargetRepositories { get; set; }

    public IList<string> Workflows { get; set; } = [];

    public string? TokenId { get; set; }
}
