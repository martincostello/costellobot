// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed partial class GitHubWebhookService(IGitHubJob job) : IHostedService
{
    private Task? _executeTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _executeTask = job.ExecuteAsync(cancellationToken);

        if (_executeTask.IsCompleted)
        {
            return _executeTask;
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await job.StopAsync(cancellationToken);
    }
}
