// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Octokit;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Models;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class PullRequestAnalyzer(
    IGitHubClientForInstallation client,
    GitCommitAnalyzer commitAnalyzer,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<PullRequestAnalyzer> logger)
{
    private readonly IOptionsMonitor<WebhookOptions> _options = options;

    public async Task<bool> IsFromCollaboratorAsync(IssueId id, string login)
        => await client.Repository.Collaborator.IsCollaborator(
            id.Repository.Owner,
            id.Repository.Name,
            login);

    public bool IsFromTrustedUser(IssueId id, PullRequestEvent message)
    {
        var pr = message.PullRequest!;
        return IsFromTrustedUser(id, pr.User.Login, pr.Draft);
    }

    public bool IsFromTrustedUser(IssueId id, SimplePullRequest pullRequest)
        => IsFromTrustedUser(id, pullRequest.User.Login, pullRequest.Draft);

    public async Task<bool> IsTrustedDependencyUpdateAsync(
        RepositoryId repository,
        string pullRequestHeadRef,
        string pullRequestHeadSha,
        string pullRequestUrl)
    {
        var commit = await client.Repository.Commit.Get(
            repository.Owner,
            repository.Name,
            pullRequestHeadSha);

        var diff = await GetDiffAsync(pullRequestUrl);

        return await commitAnalyzer.IsTrustedDependencyUpdateAsync(
            repository,
            pullRequestHeadRef,
            commit,
            diff);
    }

    private async Task<string?> GetDiffAsync(string diffUrl)
    {
        try
        {
            return await client.GetDiffAsync(diffUrl);
        }
        catch (Exception ex)
        {
            Log.GetDiffFailed(logger, ex, diffUrl);
            return null;
        }
    }

    private bool IsFromTrustedUser(IssueId id, string authorLogin, bool isDraft)
    {
        var options = _options.CurrentValue;

        if (options.IgnoreRepositories.Contains(id.Repository.FullName, StringComparer.OrdinalIgnoreCase))
        {
            Log.IgnoringPullRequestAsRepositoryIgnored(logger, id);
            return false;
        }

        if (isDraft)
        {
            Log.IgnoringPullRequestDraft(logger, id);
            return false;
        }

        bool isTrusted = _options.CurrentValue.TrustedEntities.Users.Contains(
            authorLogin,
            StringComparer.Ordinal);

        if (!isTrusted)
        {
            Log.IgnoringPullRequestFromUntrustedUser(logger, id, authorLogin);
        }

        return isTrusted;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "Ignoring pull request {PullRequest} as it is a draft.")]
        public static partial void IgnoringPullRequestDraft(
            ILogger logger,
            IssueId pullRequest);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Debug,
           Message = "Ignoring pull request {PullRequest} from {Login} as it is not from a trusted user.")]
        public static partial void IgnoringPullRequestFromUntrustedUser(
            ILogger logger,
            IssueId pullRequest,
            string? login);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Debug,
           Message = "Ignoring pull request {PullRequest} as the repository is configured to be ignored.")]
        public static partial void IgnoringPullRequestAsRepositoryIgnored(
            ILogger logger,
            IssueId pullRequest);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Warning,
           Message = "Failed to get Git diff from URL {GitDiffUrl}.")]
        public static partial void GetDiffFailed(
            ILogger logger,
            Exception exception,
            string gitDiffUrl);
    }
}
