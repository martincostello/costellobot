// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace MartinCostello.Costellobot;

[CostellobotAdmin]
public class GitHubWebhookHub(ClientLogQueue logs, GitHubWebhookQueue webhooks) : Hub<IWebhookClient>
{
    public override async Task OnConnectedAsync()
    {
        foreach (var logEntry in logs.History())
        {
            await Clients.Caller.LogAsync(logEntry);
        }

        foreach (var @event in webhooks.History())
        {
            await Clients.Caller.WebhookAsync(@event.RawHeaders, @event.RawPayload);
        }
    }
}
