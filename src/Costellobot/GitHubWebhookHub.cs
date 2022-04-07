// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace MartinCostello.Costellobot;

[CostellobotAdmin]
public class GitHubWebhookHub : Hub<IWebhookClient>
{
    private readonly ClientLogQueue _logs;
    private readonly GitHubWebhookQueue _webhooks;

    public GitHubWebhookHub(ClientLogQueue logs, GitHubWebhookQueue webhooks)
    {
        _logs = logs;
        _webhooks = webhooks;
    }

    public override async Task OnConnectedAsync()
    {
        foreach (var logEntry in _logs.History())
        {
            await Clients.Caller.LogAsync(logEntry);
        }

        foreach (var @event in _webhooks.History())
        {
            await Clients.Caller.WebhookAsync(@event.RawHeaders, @event.RawPayload);
        }
    }
}
