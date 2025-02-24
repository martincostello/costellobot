﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class GitHubOptions
{
    public string AccessToken { get; set; } = string.Empty;

    public string AppId { get; set; } = string.Empty;

    public string BadgesKey { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string EnterpriseDomain { get; set; } = string.Empty;

    public long InstallationId { get; set; }

    public string OAuthId { get; set; } = string.Empty;

    public string PrivateKey { get; set; } = string.Empty;

    public IList<string> Scopes { get; set; } = [];

    public string WebhookSecret { get; set; } = string.Empty;
}
