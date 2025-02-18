﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit.Webhooks;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class PullRequestReviewHandler : IHandler
{
    public async Task HandleAsync(WebhookEvent message)
    {
        // TODO Implement
        await Task.CompletedTask;
    }
}
