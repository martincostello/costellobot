// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace MartinCostello.Costellobot;

public sealed class ClientLogBroadcastService(ClientLogQueue queue, IHubContext<GitHubWebhookHub, IWebhookClient> context) : BackgroundService()
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var logEntry = await queue.DequeueAsync(stoppingToken);

            if (logEntry is null)
            {
                break;
            }

            await context.Clients.All.LogAsync(logEntry);
        }
    }
}
