// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit.Webhooks;

namespace MartinCostello.Costellobot.Handlers;

public sealed class HandlerFactory : IHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;

    public HandlerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IHandler Create(string? eventType)
    {
        return eventType switch
        {
            WebhookEventType.CheckSuite => _serviceProvider.GetRequiredService<CheckSuiteHandler>(),
            WebhookEventType.DeploymentProtectionRule => _serviceProvider.GetRequiredService<DeploymentProtectionRuleHandler>(),
            WebhookEventType.DeploymentStatus => _serviceProvider.GetRequiredService<DeploymentStatusHandler>(),
            WebhookEventType.PullRequest => _serviceProvider.GetRequiredService<PullRequestHandler>(),
            WebhookEventType.Push => _serviceProvider.GetRequiredService<PushHandler>(),
            _ => NullHandler.Instance,
        };
    }
}
