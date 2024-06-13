// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.IssueComment;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class IssueCommentHandler(
    IGitHubClientForInstallation client,
    ILogger<IssueCommentHandler> logger) : IHandler
{
    public async Task HandleAsync(WebhookEvent message)
    {
        if (message is not IssueCommentEvent body ||
            body.Repository is not { } repo ||
            body.Issue is not { } issue ||
            body.Comment is not { } comment)
        {
            return;
        }

        bool ignore = true;

        const string Prefix = "@costellobot ";

        if (string.Equals(message.Action, IssueCommentActionValue.Created, StringComparison.Ordinal) &&
            comment.AuthorAssociation.Value is Octokit.Webhooks.Models.AuthorAssociation.Owner &&
            comment.Body?.StartsWith(Prefix, StringComparison.Ordinal) is true)
        {
            ignore = false;
        }

        var issueId = IssueId.Create(repo, issue.Number);

        if (ignore)
        {
            Log.IgnoringCommentAction(logger, issueId, message.Action);
            return;
        }

        string command = comment.Body![Prefix.Length..].Trim();

        Log.ReceivedComment(logger, issueId, command);

        if (issue.PullRequest is not null &&
            string.Equals(command, "rebase", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await RebaseAsync(issueId, comment.Id);
            }
            catch (Exception ex)
            {
                Log.RebaseFailed(logger, ex, issueId);
            }
        }
    }

    private async Task RebaseAsync(IssueId issue, long commentId)
    {
        var pull = await client.PullRequest.Get(issue.Owner, issue.Name, issue.Number);

        if (pull.State.Value is not ItemState.Open)
        {
            return;
        }

        // See https://github.com/martincostello/github-automation/blob/main/.github/workflows/rebase-pull-request.yml
        var dispatch = new
        {
            event_type = "rebase_pull_request",
            client_payload = new
            {
                repository = issue.Repository.FullName,
                number = issue.Number,
                @base = pull.Base.Ref,
                head = pull.Head.Ref,
            },
        };

        await client.RepositoryDispatchAsync("martincostello", "github-automation", dispatch);

        Log.RebaseRequested(logger, issue);

        try
        {
            await client.Reaction.IssueComment.Create(issue.Owner, issue.Name, commentId, new NewReaction(ReactionType.Plus1));
        }
        catch (Exception ex)
        {
            Log.ReactionFailed(logger, ex, commentId, issue);
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "Ignoring comment on issue {Issue} for action {Action}.")]
        public static partial void IgnoringCommentAction(ILogger logger, IssueId issue, string? action);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Debug,
           Message = "Received comment on issue {Issue}: {Content}.")]
        public static partial void ReceivedComment(ILogger logger, IssueId issue, string? content);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Information,
           Message = "Requested rebase for pull request {PullRequest}.")]
        public static partial void RebaseRequested(ILogger logger, IssueId pullRequest);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Warning,
           Message = "Failed to rebase pull request {PullRequest}.")]
        public static partial void RebaseFailed(ILogger logger, Exception exception, IssueId pullRequest);

        [LoggerMessage(
           EventId = 5,
           Level = LogLevel.Warning,
           Message = "Failed to react to comment {CommentId} in pull request {PullRequest}.")]
        public static partial void ReactionFailed(ILogger logger, Exception exception, long commentId, IssueId pullRequest);
    }
}
