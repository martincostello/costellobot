// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Hybrid;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events.PullRequestReview;
using Octokit.Webhooks.Models.PullRequestReviewEvent;
using PullRequestMergeMethod = Octokit.GraphQL.Model.PullRequestMergeMethod;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class PullRequestReviewHandler(
    GitHubWebhookContext context,
    PullRequestAnalyzer pullRequestAnalyzer,
    PullRequestApprover pullRequestApprover,
    HybridCache cache,
    ITrustStore trustStore,
    ILogger<PullRequestReviewHandler> logger) : IHandler
{
    private const int PageSize = 100;

    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromDays(1) };
    private static readonly string[] CacheTags = ["all", "github"];
    private static readonly string[] PullRequestCreators = ["app/dependabot", "app/renovate"];

    public async Task HandleAsync(WebhookEvent message, CancellationToken cancellationToken)
    {
        if (!context.WebhookOptions.ImplicitTrust)
        {
            // Feature disabled
            return;
        }

        if (message is not PullRequestReviewSubmittedEvent body ||
            body.Repository is not { } repo ||
            body.PullRequest is not { } pr)
        {
            // Invalid payload
            return;
        }

        if (!IsApprovedByOwner(body.Review))
        {
            return;
        }

        var id = IssueId.Create(repo, pr.Number);

        if (!pullRequestAnalyzer.IsFromTrustedUser(id, pr))
        {
            return;
        }

        if (await pullRequestAnalyzer.HasValidApprovalAsync(id, pr.User.Login))
        {
            // Already approved by someone else
            return;
        }

        (var ecosystem, var dependencies) = await pullRequestAnalyzer.GetDependencyTrustAsync(
            RepositoryId.Create(repo),
            pr.Head.Ref,
            pr.Head.Sha,
            pr.Url,
            cancellationToken);

        if (dependencies.Count < 1)
        {
            return;
        }

        int trustAdditions = 0;

        foreach ((string name, (bool automaticallyTrusted, string? version)) in dependencies)
        {
            if (automaticallyTrusted || version is not { Length: > 0 })
            {
                continue;
            }

            try
            {
                // If the pull request was approved by the owner, then any
                // otherwise untrusted dependencies are now implicitly trusted.
                await trustStore.TrustAsync(ecosystem, name, version, cancellationToken);

                trustAdditions++;

                Log.DependencyImplicitlyTrusted(logger, ecosystem, name, version);
            }
            catch (Exception ex)
            {
                Log.FailedToImplicitlyTrustDependency(logger, ex, ecosystem, name, version);
            }
        }

        var options = context.WebhookOptions;

        if (trustAdditions > 0 && (options.Approve || options.Automerge))
        {
            try
            {
                await TryApproveOpenBotPullRequestsAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.FailedToApprovePullRequests(logger, ex);
            }
        }

        static bool IsApprovedByOwner(Review review) =>
            review is { AuthorAssociation.Value: Octokit.Webhooks.Models.AuthorAssociation.Owner } &&
            review.State is { Value: Octokit.Webhooks.Models.PullRequestReviewEvent.ReviewState.Approved };
    }

    private async Task<string> GetAppLoginAsync(CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"github-app-slug-{context.AppId}",
            context.AppClient.GitHubApps,
            static async (client, _) =>
            {
                var app = await client.GetCurrent();
                return $"{app.Slug}[bot]";
            },
            CacheEntryOptions,
            CacheTags,
            cancellationToken);

    private async Task<IReadOnlyList<(string Owner, string Name, long Id)>> GetInstallationRepositoriesAsync(CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"github-app-repositories-{context.AppId}",
            async (_) =>
            {
                var installationRepositories = await context.InstallationClient.GitHubApps.Installation.GetAllRepositoriesForCurrent(new() { PageSize = PageSize });

                var ignored = context.WebhookOptions.IgnoreRepositories;

                return installationRepositories.Repositories
                    .Where((p) => !p.Archived)
                    .Where((p) => !p.Fork)
                    .Where((p) => !ignored.Contains(p.FullName, StringComparer.OrdinalIgnoreCase))
                    .Select((p) => (p.Owner.Login, p.Name, p.Id))
                    .ToArray();
            },
            CacheEntryOptions,
            CacheTags,
            cancellationToken);

    private async Task<PullRequestMergeMethod> GetRepositoryMergeMethodAsync(long repositoryId, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"github-repo-{repositoryId}",
            (repositoryId, context.InstallationClient.Repository),
            static async (state, _) =>
            {
                var repository = await state.Repository.Get(state.repositoryId);
                return PullRequestApprover.GetMergeMethod(repository);
            },
            CacheEntryOptions,
            CacheTags,
            cancellationToken);

    private async Task TryApproveOpenBotPullRequestsAsync(
        IssueId triggeringId,
        CancellationToken cancellationToken)
    {
        var repositories = await GetInstallationRepositoriesAsync(cancellationToken);

        foreach (var creator in PullRequestCreators)
        {
            Log.SearchingInstallationRepositories(logger, repositories.Count, creator);

            var options = new ApiOptions() { PageSize = PageSize };
            var request = new RepositoryIssueRequest()
            {
                Creator = creator,
                Filter = IssueFilter.Created,
                State = ItemStateFilter.Open,
            };

            await Parallel.ForEachAsync(repositories, cancellationToken, async (repo, token) =>
            {
                var issues = await context.InstallationClient.Issue.GetAllForRepository(repo.Id, request, options);

                var issuesWithPulls = issues
                    .Where((p) => p.PullRequest is { })
                    .ToArray();

                var repositoryId = new RepositoryId(repo.Owner, repo.Name);

                Log.FoundPullRequests(logger, issuesWithPulls.Length, creator, repositoryId);

                if (issuesWithPulls.Length < 1)
                {
                    return;
                }

                PullRequestMergeMethod? mergeMethod = default;

                foreach (var issue in issuesWithPulls)
                {
                    var pullId = new IssueId(repositoryId, issue.Number);

                    if (pullId == triggeringId)
                    {
                        continue;
                    }

                    var appLogin = await GetAppLoginAsync(token);

                    if (await pullRequestAnalyzer.IsApprovedByAsync(pullId, appLogin))
                    {
                        Log.PullRequestAlreadyApproved(logger, pullId, appLogin);
                    }
                    else
                    {
                        var pull = await context.InstallationClient.PullRequest.Get(repositoryId.Owner, repositoryId.Name, issue.Number);

                        bool isTrusted = await pullRequestAnalyzer.IsTrustedDependencyUpdateAsync(
                            repositoryId,
                            pull.Head.Ref,
                            pull.Head.Sha,
                            pull.Url,
                            token);

                        if (isTrusted)
                        {
                            mergeMethod ??= await GetRepositoryMergeMethodAsync(repo.Id, token);

                            await pullRequestApprover.ApproveAndMergeAsync(
                                pullId,
                                pull.NodeId,
                                mergeMethod.GetValueOrDefault(),
                                token);

                            Log.PullRequestApprovedAfterImplicitTrust(logger, pullId);
                        }
                    }
                }
            });
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Dependency with Id {Dependency} and version {Version} from ecosystem {Ecosystem} was implicitly trusted.")]
        public static partial void DependencyImplicitlyTrusted(
            ILogger logger,
            DependencyEcosystem ecosystem,
            string dependency,
            string version);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "Failed to implicitly trust dependency with Id {Dependency} and version {Version} from ecosystem {Ecosystem}.")]
        public static partial void FailedToImplicitlyTrustDependency(
            ILogger logger,
            Exception exception,
            DependencyEcosystem ecosystem,
            string dependency,
            string version);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Failed to approve open pull requests.")]
        public static partial void FailedToApprovePullRequests(
            ILogger logger,
            Exception exception);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "Searching {Count} installation repositories for pull requests from {Creator}.")]
        public static partial void SearchingInstallationRepositories(
            ILogger logger,
            int count,
            string creator);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Debug,
            Message = "Found {Count} pull requests created by {Creator} in repository {Repository}.")]
        public static partial void FoundPullRequests(
            ILogger logger,
            int count,
            string creator,
            RepositoryId repository);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Debug,
            Message = "Pull request {PullRequest} was already approved by {Actor}.")]
        public static partial void PullRequestAlreadyApproved(
            ILogger logger,
            IssueId pullRequest,
            string actor);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Debug,
            Message = "Pull request {PullRequest} was approved after dependencies were implicitly trusted.")]
        public static partial void PullRequestApprovedAfterImplicitTrust(
            ILogger logger,
            IssueId pullRequest);
    }
}
