// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace MartinCostello.Costellobot;

public sealed class ClientLogBroadcastService : BackgroundService
{
    private readonly ClientLogQueue _queue;
    private readonly IHubContext<GitHubWebhookHub, IWebhookClient> _context;

    public ClientLogBroadcastService(ClientLogQueue queue, IHubContext<GitHubWebhookHub, IWebhookClient> context)
        : base()
    {
        _queue = queue;
        _context = context;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var logEntry = await _queue.DequeueAsync(stoppingToken);

            if (logEntry is null)
            {
                break;
            }

            await _context.Clients.All.LogAsync(logEntry);
        }
    }
}
