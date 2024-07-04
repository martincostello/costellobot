// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed partial class InMemoryGitHubEventHandler(GitHubWebhookQueue queue) : IGitHubEventHandler
{
    public Task HandleAsync(GitHubEvent payload, CancellationToken cancellationToken)
    {
        queue.Enqueue(payload);
        return Task.CompletedTask;
    }
}
