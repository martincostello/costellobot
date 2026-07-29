// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

/// <summary>
/// The names of the SignalR methods invoked on connected clients by <see cref="GitHubWebhookHub"/>.
/// </summary>
/// <remarks>
/// Names must stay in sync with <c>App.ts</c>.
/// </remarks>
public static class WebhookClientMethods
{
    /// <summary>
    /// The name of the method invoked to send an application log entry to a client.
    /// </summary>
    public const string Log = "application-logs";

    /// <summary>
    /// The name of the method invoked to send a webhook delivery to a client.
    /// </summary>
    public const string Webhook = "webhook-logs";
}
