// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class RegistryOptions
{
    public Uri BaseAddress { get; set; } = default!;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}
