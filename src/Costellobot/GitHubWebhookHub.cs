// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace MartinCostello.Costellobot;

[Authorization.CostellobotAdmin]
public class GitHubWebhookHub(ClientLogQueue logs, GitHubWebhookQueue webhooks) : Hub
{
    public override async Task OnConnectedAsync()
    {
        foreach (var logEntry in logs.History())
        {
            await Clients.Caller.SendAsync(WebhookClientMethods.Log, logEntry, Context.ConnectionAborted);
        }

        foreach (var @event in webhooks.History())
        {
            await Clients.Caller.SendAsync(WebhookClientMethods.Webhook, @event.RawHeaders, @event.RawPayload, Context.ConnectionAborted);
        }
    }
}
