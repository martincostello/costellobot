﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.Options;
using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.PullRequest;
using IConnection = Octokit.GraphQL.IConnection;
using PullRequestMergeMethod = Octokit.GraphQL.Model.PullRequestMergeMethod;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class PullRequestHandler(
    IGitHubClientForInstallation client,
    IConnection connection,
    GitCommitAnalyzer commitAnalyzer,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<PullRequestHandler> logger) : IHandler
{
    private readonly IOptionsMonitor<WebhookOptions> _options = options;

    public async Task HandleAsync(WebhookEvent message)
    {
        if (message is not PullRequestEvent body ||
            body.Repository is not { } repo ||
            body.PullRequest is not { } pr)
        {
            return;
        }

        bool isManualApproval = false;

        if (!IsNewPullRequestFromTrustedUser(body))
        {
            if (!await IsManuallyApprovedAsync(body))
            {
                return;
            }

            isManualApproval = true;
        }

        string owner = repo.Owner.Login;
        string name = repo.Name;
        int number = (int)pr.Number;

        bool isTrusted = false;

        if (!isManualApproval)
        {
            isTrusted = await IsTrustedDependencyUpdateAsync(body);
        }

        if (isManualApproval || isTrusted)
        {
            var options = _options.CurrentValue;

            if (options.Approve)
            {
                await ApproveAsync(owner, name, number);
            }

            if (options.Automerge)
            {
                await EnableAutoMergeAsync(
                    owner,
                    name,
                    number,
                    pr.NodeId,
                    GetMergeMethod(pr.Base.Repo));
            }
        }
    }

    private static PullRequestMergeMethod GetMergeMethod(Octokit.Webhooks.Models.Repository repo)
    {
        if (repo.AllowMergeCommit == true)
        {
            return PullRequestMergeMethod.Merge;
        }
        else if (repo.AllowSquashMerge == true)
        {
            return PullRequestMergeMethod.Squash;
        }
        else if (repo.AllowRebaseMerge == true)
        {
            return PullRequestMergeMethod.Rebase;
        }
        else
        {
            return PullRequestMergeMethod.Merge;
        }
    }

    private async Task ApproveAsync(
        string owner,
        string name,
        int number)
    {
        var body = new StringBuilder(_options.CurrentValue.ApproveComment);

        if (body.Length > 0)
        {
            body.Append('\n')
                .Append('\n')
                .Append("<!-- Generated by version ")
                .Append(GitMetadata.Version)
                .Append(" of Costellobot -->");
        }

        await client.PullRequest.Review.Create(
            owner,
            name,
            number,
            new()
            {
                Body = body.ToString(),
                Event = Octokit.PullRequestReviewEvent.Approve,
            });

        Log.PullRequestApproved(logger, owner, name, number);
    }

    private async Task EnableAutoMergeAsync(
        string owner,
        string name,
        int number,
        string nodeId,
        PullRequestMergeMethod mergeMethod)
    {
        var input = new EnablePullRequestAutoMergeInput()
        {
            MergeMethod = mergeMethod,
            PullRequestId = new(nodeId),
        };

        var mutation = new Mutation()
            .EnablePullRequestAutoMerge(input)
            .Select((p) => new { p.PullRequest.Number })
            .Compile();

        try
        {
            await connection.Run(mutation);
            Log.AutoMergeEnabled(logger, owner, name, number);
        }
        catch (Octokit.GraphQL.Core.Deserializers.ResponseDeserializerException ex) when (ex.Message.Contains("Pull request Pull request is in clean status", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // If auto-merge failed as the PR is ready to merge, then just merge it
                var response = await client.PullRequest.Merge(owner, name, number, new()
                {
                    MergeMethod = Enum.Parse<Octokit.PullRequestMergeMethod>(mergeMethod.ToString()),
                });

                if (response.Merged)
                {
                    Log.PullRequestMerged(logger, owner, name, number);
                }
            }
            catch (Exception ex2)
            {
                Log.EnableAutoMergeFailed(logger, ex, owner, name, number, nodeId);
                Log.MergeFailed(logger, ex2, owner, name, number);
            }
        }
        catch (Exception ex)
        {
            Log.EnableAutoMergeFailed(logger, ex, owner, name, number, nodeId);
        }
    }

    private bool IsNewPullRequestFromTrustedUser(PullRequestEvent message)
    {
        string owner = message.Repository!.Owner.Login;
        string name = message.Repository.Name;
        int number = (int)message.PullRequest!.Number;

        if (!string.Equals(message.Action, PullRequestActionValue.Opened, StringComparison.Ordinal))
        {
            if (!string.Equals(message.Action, PullRequestActionValue.Labeled, StringComparison.Ordinal))
            {
                Log.IgnoringPullRequestAction(logger, owner, name, number, message.Action);
            }

            return false;
        }

        var options = _options.CurrentValue;

        if (options.IgnoreRepositories.Contains($"{owner}/{name}", StringComparer.OrdinalIgnoreCase))
        {
            Log.IgnoringPullRequestAsRepositoryIgnored(logger, owner, name, number);
            return false;
        }

        if (message.PullRequest is not { } pr || pr.Draft)
        {
            Log.IgnoringPullRequestDraft(logger, owner, name, number);
            return false;
        }

        bool isTrusted = _options.CurrentValue.TrustedEntities.Users.Contains(
            pr.User.Login,
            StringComparer.Ordinal);

        if (!isTrusted)
        {
            Log.IgnoringPullRequestFromUntrustedUser(logger, owner, name, number, message.PullRequest.User.Login);
        }

        return isTrusted;
    }

    private async Task<bool> IsManuallyApprovedAsync(PullRequestEvent message)
    {
        if (message is not PullRequestLabeledEvent labelled ||
            message.Repository is not { } repository ||
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

        string owner = repository.Owner.Login;
        string name = repository.Name;
        string actor = sender.Login;

        bool isCollaborator = await client.Repository.Collaborator.IsCollaborator(
            owner,
            name,
            actor);

        if (isCollaborator)
        {
            Log.PullRequestManuallyApproved(
                logger,
                owner,
                name,
                (int)message.PullRequest!.Number,
                actor);
        }

        return isCollaborator;
    }

    private async Task<bool> IsTrustedDependencyUpdateAsync(PullRequestEvent message)
    {
        string owner = message.Repository!.Owner.Login;
        string name = message.Repository.Name;

        var commit = await client.Repository.Commit.Get(
            owner,
            name,
            message.PullRequest.Head.Sha);

        return await commitAnalyzer.IsTrustedDependencyUpdateAsync(
            owner,
            name,
            message.PullRequest.Head.Ref,
            commit);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Ignoring pull request {Owner}/{Repository}#{Number} for action {Action}.")]
        public static partial void IgnoringPullRequestAction(
            ILogger logger,
            string? owner,
            string? repository,
            long? number,
            string? action);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Information,
           Message = "Ignoring pull request {Owner}/{Repository}#{Number} as it is a draft.")]
        public static partial void IgnoringPullRequestDraft(
            ILogger logger,
            string? owner,
            string? repository,
            long? number);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Information,
           Message = "Ignoring pull request {Owner}/{Repository}#{Number} from {Login} as it is not from a trusted user.")]
        public static partial void IgnoringPullRequestFromUntrustedUser(
            ILogger logger,
            string? owner,
            string? repository,
            long? number,
            string? login);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Information,
           Message = "Approved pull request {Owner}/{Repository}#{Number}.")]
        public static partial void PullRequestApproved(
            ILogger logger,
            string owner,
            string repository,
            long number);

        [LoggerMessage(
           EventId = 5,
           Level = LogLevel.Information,
           Message = "Enabled auto-merge for pull request {Owner}/{Repository}#{Number}.")]
        public static partial void AutoMergeEnabled(
            ILogger logger,
            string owner,
            string repository,
            long number);

        [LoggerMessage(
           EventId = 6,
           Level = LogLevel.Warning,
           Message = "Failed to enable auto-merge for pull request {Owner}/{Repository}#{Number} with node ID {NodeId}.")]
        public static partial void EnableAutoMergeFailed(
            ILogger logger,
            Exception exception,
            string owner,
            string repository,
            long number,
            string nodeId);

        [LoggerMessage(
           EventId = 7,
           Level = LogLevel.Information,
           Message = "Ignoring pull request {Owner}/{Repository}#{Number} as the repository is configured to be ignored.")]
        public static partial void IgnoringPullRequestAsRepositoryIgnored(
            ILogger logger,
            string? owner,
            string? repository,
            long? number);

        [LoggerMessage(
           EventId = 8,
           Level = LogLevel.Information,
           Message = "Pull request {Owner}/{Repository}#{Number} was manually approved by {Actor}.")]
        public static partial void PullRequestManuallyApproved(
            ILogger logger,
            string owner,
            string repository,
            long number,
            string actor);

        [LoggerMessage(
           EventId = 9,
           Level = LogLevel.Information,
           Message = "Pull request {Owner}/{Repository}#{Number} merged.")]
        public static partial void PullRequestMerged(
            ILogger logger,
            string owner,
            string repository,
            long number);

        [LoggerMessage(
           EventId = 10,
           Level = LogLevel.Warning,
           Message = "Failed to merge pull request {Owner}/{Repository}#{Number}.")]
        public static partial void MergeFailed(
            ILogger logger,
            Exception exception,
            string owner,
            string repository,
            long number);
    }
}
