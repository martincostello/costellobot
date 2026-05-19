// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class GitHubOpenIdConnectOptions
{
    public IList<string> Audiences { get; set; } = [];

    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(1);

    public string Issuer { get; set; } = string.Empty;

    public string MetadataUri { get; set; } = string.Empty;
}
