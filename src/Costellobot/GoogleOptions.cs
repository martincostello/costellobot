// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class GoogleOptions
{
    public IList<string> CalendarIds { get; set; } = [];

    public string ClientEmail { get; set; } = string.Empty;

    public string PrivateKeyId { get; set; } = string.Empty;

    public string PrivateKey { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public IList<string> Scopes { get; set; } = [];
}
