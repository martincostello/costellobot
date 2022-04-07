// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public record GitHubEvent(
    WebhookHeaders Headers,
    WebhookEvent Event,
    IDictionary<string, string> RawHeaders,
    JsonElement RawPayload);
