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

        string owner = repo.Owner.Login;
        string name = repo.Name;
        int number = (int)issue.Number;
        long commentId = comment.Id;

        bool ignore = true;

        const string Prefix = "@costellobot ";

        if (string.Equals(message.Action, IssueCommentActionValue.Created, StringComparison.Ordinal) &&
            comment.AuthorAssociation.Value is Octokit.Webhooks.Models.AuthorAssociation.Owner &&
            comment.Body?.StartsWith(Prefix, StringComparison.Ordinal) is true)
        {
            ignore = false;
        }

        if (ignore)
        {
            Log.IgnoringCommentAction(logger, owner, name, number, message.Action);
            return;
        }

        string command = comment.Body![Prefix.Length..].Trim();

        Log.ReceivedComment(logger, owner, name, number, command);

        if (issue.PullRequest is not null &&
            string.Equals(command, "rebase", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await RebaseAsync(owner, name, number, commentId);
            }
            catch (Exception ex)
            {
                Log.RebaseFailed(logger, ex, owner, name, number);
            }
        }
    }

    private async Task RebaseAsync(string owner, string name, int number, long commentId)
    {
        var pull = await client.PullRequest.Get(owner, name, number);

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
                repository = $"{owner}/{name}",
                number,
                @base = pull.Base.Ref,
                head = pull.Head.Ref,
            },
        };

        await client.RepositoryDispatchAsync("martincostello", "github-automation", dispatch);

        Log.RebaseRequested(logger, owner, name, number);

        try
        {
            await client.Reaction.IssueComment.Create(owner, name, commentId, new NewReaction(ReactionType.Plus1));
        }
        catch (Exception ex)
        {
            Log.ReactionFailed(logger, ex, commentId, owner, name, number);
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "Ignoring comment on issue {Owner}/{Repository}#{Number} for action {Action}.")]
        public static partial void IgnoringCommentAction(
            ILogger logger,
            string? owner,
            string? repository,
            long? number,
            string? action);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Debug,
           Message = "Received comment on issue {Owner}/{Repository}#{Number}: {Content}.")]
        public static partial void ReceivedComment(
            ILogger logger,
            string? owner,
            string? repository,
            long? number,
            string? content);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Information,
           Message = "Requested rebase for pull request {Owner}/{Repository}#{Number}.")]
        public static partial void RebaseRequested(
            ILogger logger,
            string owner,
            string repository,
            long number);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Warning,
           Message = "Failed to rebase pull request {Owner}/{Repository}#{Number}.")]
        public static partial void RebaseFailed(
            ILogger logger,
            Exception exception,
            string owner,
            string repository,
            long number);

        [LoggerMessage(
           EventId = 5,
           Level = LogLevel.Warning,
           Message = "Failed to react to comment {CommentId} in pull request {Owner}/{Repository}#{Number}.")]
        public static partial void ReactionFailed(
            ILogger logger,
            Exception exception,
            long commentId,
            string owner,
            string repository,
            long number);
    }
}
