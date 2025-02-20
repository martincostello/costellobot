// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit.Webhooks;
using Octokit.Webhooks.Events;

namespace MartinCostello.Costellobot;

public static class ILoggerExtensions
{
    public static IDisposable? BeginWebhookScope(this ILogger logger, WebhookHeaders headers)
    {
        var items = new Dictionary<string, object>(2);

        if (headers.Delivery is { Length: > 0 } delivery)
        {
            items["GitHub.Delivery"] = delivery;
        }

        if (headers.Event is { Length: > 0 } @event)
        {
            items["GitHub.Event"] = @event;
        }

        return logger.BeginScope(items);
    }

    public static IDisposable? BeginWebhookScope(this ILogger logger, WebhookEvent payload)
    {
        var items = new Dictionary<string, object>(2);

        if (payload.Repository is { } repository)
        {
            items["GitHub.Repository.HtmlUrl"] = repository.HtmlUrl;
            items["GitHub.Repository.Name"] = repository.FullName;
        }

        if (payload.Sender is { } sender)
        {
            items["GitHub.Sender.HtmlUrl"] = sender.HtmlUrl;
            items["GitHub.Sender.Login"] = sender.Login;
        }

        long? pullNumber = null;
        string? pullUrl = null;

        if (payload is DeploymentStatusEvent { } deployment)
        {
            items["GitHub.Deployment.Environment"] = deployment.Deployment.Environment;
        }
        else if (payload is IssueCommentEvent { Issue.PullRequest: not null } issue)
        {
            pullNumber = issue.Issue.Number;
            pullUrl = issue.Issue.PullRequest.HtmlUrl;
        }
        else if (payload is PullRequestEvent { PullRequest: not null } pull)
        {
            pullNumber = pull.Number;
            pullUrl = pull.PullRequest.HtmlUrl;
        }
        else if (payload is PullRequestReviewEvent { PullRequest: not null } review)
        {
            pullNumber = review.PullRequest.Number;
            pullUrl = review.PullRequest.HtmlUrl;
        }
        else if (payload is PushEvent { Ref.Length: > 0 } push)
        {
            items["GitHub.Push.Ref"] = push.Ref;
        }

        if (pullNumber is { } number)
        {
            items["GitHub.PullRequest.Number"] = number.ToString(CultureInfo.InvariantCulture);
        }

        if (pullUrl is { Length: > 0 })
        {
            items["GitHub.PullRequest.HtmlUrl"] = pullUrl;
        }

        return logger.BeginScope(items);
    }
}
