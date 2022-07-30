// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

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

public sealed partial class PullRequestHandler : IHandler
{
    private readonly IGitHubClient _client;
    private readonly GitCommitAnalyzer _commitAnalyzer;
    private readonly IConnection _connection;
    private readonly IOptionsMonitor<WebhookOptions> _options;
    private readonly ILogger _logger;

    public PullRequestHandler(
        IGitHubClientForInstallation client,
        IConnection connection,
        GitCommitAnalyzer commitAnalyzer,
        IOptionsMonitor<WebhookOptions> options,
        ILogger<PullRequestHandler> logger)
    {
        _client = client;
        _connection = connection;
        _commitAnalyzer = commitAnalyzer;
        _options = options;
        _logger = logger;
    }

    public async Task HandleAsync(WebhookEvent message)
    {
        if (message is not PullRequestEvent body ||
            body.Repository is null)
        {
            return;
        }

        if (!IsNewPullRequestFromTrustedUser(body))
        {
            return;
        }

        string owner = body.Repository!.Owner.Login;
        string name = body.Repository.Name;
        int number = (int)body.PullRequest!.Number;

        bool isTrusted = await IsTrustedDependencyUpdateAsync(body);

        if (isTrusted)
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
                    body.PullRequest.NodeId,
                    GetMergeMethod(body.PullRequest.Base.Repo));
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
        await _client.PullRequest.Review.Create(
            owner,
            name,
            number,
            new()
            {
                Body = _options.CurrentValue.ApproveComment,
                Event = Octokit.PullRequestReviewEvent.Approve,
            });

        Log.PullRequestApproved(_logger, owner, name, number);
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

        var query = new Mutation()
            .EnablePullRequestAutoMerge(input)
            .Select((p) => new { p.PullRequest.Number })
            .Compile();

        try
        {
            await _connection.Run(query);
            Log.AutoMergeEnabled(_logger, owner, name, number);
        }
        catch (Exception ex)
        {
            Log.EnableAutoMergeFailed(_logger, ex, owner, name, number, nodeId);
        }
    }

    private bool IsNewPullRequestFromTrustedUser(PullRequestEvent message)
    {
        if (!string.Equals(message.Action, PullRequestActionValue.Opened, StringComparison.Ordinal))
        {
            Log.IgnoringPullRequestAction(
                _logger,
                message.Repository!.Owner.Login,
                message.Repository.Name,
                message.PullRequest.Number,
                message.Action);

            return false;
        }

        if (message.PullRequest is not { } pr || pr.Draft)
        {
            Log.IgnoringPullRequestDraft(
                _logger,
                message.Repository!.Owner.Login,
                message.Repository.Name,
                message.PullRequest.Number);

            return false;
        }

        bool isTrusted = _options.CurrentValue.TrustedEntities.Users.Contains(
            pr.User.Login,
            StringComparer.Ordinal);

        if (!isTrusted)
        {
            Log.IgnoringPullRequestFromUntrustedUser(
                _logger,
                message.Repository!.Owner.Login,
                message.Repository.Name,
                message.PullRequest.Number,
                message.PullRequest.User.Login);
        }

        return isTrusted;
    }

    private async Task<bool> IsTrustedDependencyUpdateAsync(PullRequestEvent message)
    {
        string owner = message.Repository!.Owner.Login;
        string name = message.Repository.Name;

        var commit = await _client.Repository.Commit.Get(
            owner,
            name,
            message.PullRequest.Head.Sha);

        return await _commitAnalyzer.IsTrustedDependencyUpdateAsync(
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
    }
}
