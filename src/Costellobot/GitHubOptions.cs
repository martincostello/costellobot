// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class GitHubOptions
{
    public string AppId { get; set; } = string.Empty;

    public string EnterpriseDomain { get; set; } = string.Empty;

    public long InstallationId { get; set; }

    public string PrivateKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;
}
