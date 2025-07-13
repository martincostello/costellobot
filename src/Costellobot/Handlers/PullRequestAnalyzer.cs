// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Models;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class PullRequestAnalyzer(
    GitHubWebhookContext context,
    GitCommitAnalyzer commitAnalyzer,
    ILogger<PullRequestAnalyzer> logger)
{
    public async Task<(DependencyEcosystem Ecosystem, IDictionary<string, (bool Trusted, string? Version)> Dependencies)> GetDependencyTrustAsync(
        RepositoryId repository,
        string pullRequestHeadRef,
        string pullRequestHeadSha,
        string pullRequestUrl,
        CancellationToken cancellationToken)
    {
        var commit = await context.InstallationClient.Repository.Commit.Get(
            repository.Owner,
            repository.Name,
            pullRequestHeadSha);

        var diff = await GetDiffAsync(pullRequestUrl, cancellationToken);

        return await commitAnalyzer.GetDependencyTrustAsync(
            repository,
            pullRequestHeadRef,
            commit,
            diff,
            cancellationToken);
    }

    public async Task<bool> HasValidApprovalAsync(IssueId id, string authorLogin)
    {
        var reviews = await context.InstallationClient.PullRequest.Review.GetAll(
            id.Repository.Owner,
            id.Repository.Name,
            id.Number);

        if (reviews.Count < 1)
        {
            return false;
        }

        var options = context.WebhookOptions;

        return reviews.Any((p) =>
            p.State is { Value: PullRequestReviewState.Approved } &&
            p.User.Login != authorLogin &&
            options.TrustedEntities.Reviewers.Contains(p.User.Login, StringComparer.Ordinal));
    }

    public async Task<bool> IsApprovedByAsync(IssueId id, string authorLogin)
    {
        var reviews = await context.InstallationClient.PullRequest.Review.GetAll(
            id.Repository.Owner,
            id.Repository.Name,
            id.Number);

        return reviews.Any((p) => p.State is { Value: PullRequestReviewState.Approved } && p.User.Login == authorLogin);
    }

    public async Task<bool> IsFromCollaboratorAsync(IssueId id, string login)
        => await context.InstallationClient.Repository.Collaborator.IsCollaborator(
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
        string pullRequestUrl,
        CancellationToken cancellationToken)
    {
        var commit = await context.InstallationClient.Repository.Commit.Get(
            repository.Owner,
            repository.Name,
            pullRequestHeadSha);

        var diff = await GetDiffAsync(pullRequestUrl, cancellationToken);

        return await commitAnalyzer.IsTrustedDependencyUpdateAsync(
            repository,
            pullRequestHeadRef,
            commit,
            diff,
            cancellationToken);
    }

    private async Task<string?> GetDiffAsync(
        string diffUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            return await context.InstallationClient.GetDiffAsync(diffUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.GetDiffFailed(logger, ex, diffUrl);
            return null;
        }
    }

    private bool IsFromTrustedUser(IssueId id, string authorLogin, bool isDraft)
    {
        var options = context.WebhookOptions;

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

        bool isTrusted = options.TrustedEntities.Users.Contains(
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
