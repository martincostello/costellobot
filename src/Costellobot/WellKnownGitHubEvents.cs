// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit.Webhooks;
using Octokit.Webhooks.Events.CheckSuite;
using Octokit.Webhooks.Events.DeploymentProtectionRule;
using Octokit.Webhooks.Events.DeploymentStatus;
using Octokit.Webhooks.Events.IssueComment;
using Octokit.Webhooks.Events.PullRequest;
using Octokit.Webhooks.Events.PullRequestReview;

namespace MartinCostello.Costellobot;

/// <summary>
/// Defines all of the known/consumed GitHub webhook events.
/// </summary>
public static class WellKnownGitHubEvents
{
    private static readonly HashSet<(string? Event, string? Action)> KnownEvents =
    [
        (WebhookEventType.CheckSuite, CheckSuiteActionValue.Completed),
        (WebhookEventType.DeploymentProtectionRule, DeploymentProtectionRuleActionValue.Requested),
        (WebhookEventType.DeploymentStatus, DeploymentStatusActionValue.Created),
        (WebhookEventType.IssueComment, IssueCommentActionValue.Created),
        (WebhookEventType.Ping, null),
        (WebhookEventType.Push, null),
        (WebhookEventType.PullRequest, PullRequestActionValue.Labeled),
        (WebhookEventType.PullRequest, PullRequestActionValue.Opened),
        (WebhookEventType.PullRequestReview, PullRequestReviewActionValue.Submitted),
        (WebhookEventType.RepositoryDispatch, RepositoryDispatchActionValue.DeploymentCompleted),
        (WebhookEventType.RepositoryDispatch, RepositoryDispatchActionValue.DeploymentStarted),
    ];

    public static bool IsKnown(GitHubEvent message)
        => KnownEvents.Contains((message.Headers.Event, message.Event.Action));

    public static class RepositoryDispatchActionValue
    {
        public const string DeploymentCompleted = "deployment_completed";
        public const string DeploymentStarted = "deployment_started";
    }
}
