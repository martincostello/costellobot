// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public interface IWebhookClient
{
    [HubMethodName("application-logs")]
    Task LogAsync(object logEntry);

    [HubMethodName("webhook-logs")]
    Task WebhookAsync(WebhookHeaders headers, WebhookEvent webhookEvent);
}
