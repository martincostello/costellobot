// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Models;

public sealed record TrustedDependency(
    string Id,
    string Version)
{
    public DateTimeOffset? TrustedAt { get; set; }
}
