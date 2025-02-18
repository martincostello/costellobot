// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Octokit.GraphQL;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.PullRequest;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class PullRequestHandler(
    PullRequestAnalyzer pullRequestAnalyzer,
    PullRequestApprover approver,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<PullRequestHandler> logger) : IHandler
{
    private readonly IOptionsMonitor<WebhookOptions> _options = options;

    public async Task HandleAsync(WebhookEvent message)
    {
        if (message is not PullRequestEvent body ||
            body.Repository is null ||
            body.PullRequest is not { } pr)
        {
            return;
        }

        bool isManualApproval = false;

        if (!IsNewPullRequestFromTrustedUser(body, out var pull))
        {
            if (!await IsManuallyApprovedAsync(pull, body))
            {
                return;
            }

            isManualApproval = true;
        }

        bool isTrusted = false;

        if (!isManualApproval)
        {
            isTrusted = await pullRequestAnalyzer.IsTrustedDependencyUpdateAsync(
                pull.Repository,
                pr.Head.Ref,
                pr.Head.Sha,
                pr.Url);
        }

        if (isManualApproval || isTrusted)
        {
            await approver.ApproveAndMergeAsync(pull, pr.NodeId, pr.Base.Repo);
        }
    }

    private bool IsNewPullRequestFromTrustedUser(PullRequestEvent message, out IssueId pull)
    {
        pull = IssueId.Create(message.Repository!, message.PullRequest!.Number);

        if (!string.Equals(message.Action, PullRequestActionValue.Opened, StringComparison.Ordinal))
        {
            if (!string.Equals(message.Action, PullRequestActionValue.Labeled, StringComparison.Ordinal))
            {
                Log.IgnoringPullRequestAction(logger, pull, message.Action);
            }

            return false;
        }

        return pullRequestAnalyzer.IsFromTrustedUser(pull, message);
    }

    private async Task<bool> IsManuallyApprovedAsync(IssueId pull, PullRequestEvent message)
    {
        if (message is not PullRequestLabeledEvent labelled ||
            message.Sender is not { } sender ||
            message.PullRequest.State?.Value != Octokit.Webhooks.Models.PullRequestEvent.PullRequestState.Open ||
            message.PullRequest.Draft)
        {
            return false;
        }

        var comparer = StringComparer.Ordinal;
        var options = _options.CurrentValue;
        var presentLabels = message.PullRequest.Labels.Select((p) => p.Name).ToHashSet(comparer);
        var requiredLabels = options.ApproveLabels.Intersect(options.AutomergeLabels).ToHashSet(comparer);

        if (presentLabels.Count < 1 ||
            requiredLabels.Count < 1 ||
            !presentLabels.Intersect(requiredLabels).SequenceEqual(requiredLabels, comparer))
        {
            // All of the required labels are not present
            return false;
        }

        string actor = sender.Login;

        bool isCollaborator = await pullRequestAnalyzer.IsFromCollaboratorAsync(pull, actor);

        if (isCollaborator)
        {
            Log.PullRequestManuallyApproved(
                logger,
                pull,
                actor);
        }

        return isCollaborator;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "Ignoring pull request {PullRequest} for action {Action}.")]
        public static partial void IgnoringPullRequestAction(
            ILogger logger,
            IssueId pullRequest,
            string? action);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Information,
           Message = "Pull request {PullRequest} was manually approved by {Actor}.")]
        public static partial void PullRequestManuallyApproved(
            ILogger logger,
            IssueId pullRequest,
            string actor);
    }
}
