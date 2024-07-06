// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public static class ILoggerExtensions
{
    public static IDisposable? BeginScope(this ILogger logger, WebhookHeaders headers)
    {
        var items = new Dictionary<string, object>(2)
        {
            ["GitHubDelivery"] = headers.Delivery ?? string.Empty,
            ["GitHubEvent"] = headers.Event ?? string.Empty,
        };

        return logger.BeginScope(items);
    }
}
