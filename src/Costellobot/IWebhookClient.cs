// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.AspNetCore.SignalR;

namespace MartinCostello.Costellobot;

public interface IWebhookClient
{
    [HubMethodName("application-logs")]
    Task LogAsync(ClientLogMessage logEntry);

    [HubMethodName("webhook-logs")]
    Task WebhookAsync(IDictionary<string, string> headers, JsonElement webhookEvent);
}
