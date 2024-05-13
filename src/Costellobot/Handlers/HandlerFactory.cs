// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit.Webhooks;

namespace MartinCostello.Costellobot.Handlers;

public sealed class HandlerFactory(IServiceProvider serviceProvider) : IHandlerFactory
{
    public IHandler Create(string? eventType)
    {
        return eventType switch
        {
            WebhookEventType.CheckSuite => serviceProvider.GetRequiredService<CheckSuiteHandler>(),
            WebhookEventType.DeploymentProtectionRule => serviceProvider.GetRequiredService<DeploymentProtectionRuleHandler>(),
            WebhookEventType.DeploymentStatus => serviceProvider.GetRequiredService<DeploymentStatusHandler>(),
            WebhookEventType.IssueComment => serviceProvider.GetRequiredService<IssueCommentHandler>(),
            WebhookEventType.PullRequest => serviceProvider.GetRequiredService<PullRequestHandler>(),
            WebhookEventType.Push => serviceProvider.GetRequiredService<PushHandler>(),
            _ => NullHandler.Instance,
        };
    }
}
