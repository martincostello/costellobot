// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;

namespace MartinCostello.Costellobot.Models;

#pragma warning disable CA1724

public sealed record ConfigurationModel(
    GitHubOptions GitHub,
    WebhookOptions Webhook,
    MiscellaneousRateLimit? InstallationRateLimits,
    MiscellaneousRateLimit? UserRateLimits);
