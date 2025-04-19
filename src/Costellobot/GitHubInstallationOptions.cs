// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class GitHubInstallationOptions
{
    public string AppId { get; set; } = string.Empty;

    public string? Organization { get; set; }
}
