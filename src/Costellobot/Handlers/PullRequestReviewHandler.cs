// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events.PullRequestReview;
using Octokit.Webhooks.Models.PullRequestReviewEvent;
using PullRequestMergeMethod = Octokit.GraphQL.Model.PullRequestMergeMethod;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class PullRequestReviewHandler(
    IGitHubClientForApp appClient,
    IGitHubClientForInstallation installationClient,
    PullRequestAnalyzer pullRequestAnalyzer,
    PullRequestApprover pullRequestApprover,
    IMemoryCache cache,
    ITrustStore trustStore,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<PullRequestReviewHandler> logger) : IHandler
{
    private const int PageSize = 100;

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(1);

    public async Task HandleAsync(WebhookEvent message)
    {
        if (!options.CurrentValue.ImplicitTrust)
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
            pr.Url);

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
                await trustStore.TrustAsync(ecosystem, name, version);

                trustAdditions++;

                Log.DependencyImplicitlyTrusted(logger, ecosystem, name, version);
            }
            catch (Exception ex)
            {
                Log.FailedToImplicitlyTrustDependency(logger, ex, ecosystem, name, version);
            }
        }

        if (trustAdditions > 0 && (options.CurrentValue.Approve || options.CurrentValue.Automerge))
        {
            try
            {
                await TryApproveOpenDependabotPullRequestsAsync(id);
            }
            catch (Exception ex)
            {
                Log.FailedToApproveDependabotPullRequests(logger, ex);
            }
        }

        static bool IsApprovedByOwner(Review review) =>
            review is { AuthorAssociation.Value: Octokit.Webhooks.Models.AuthorAssociation.Owner } &&
            review?.State is { Value: Octokit.Webhooks.Models.PullRequestReviewEvent.ReviewState.Approved };
    }

    private async Task<string> GetAppLoginAsync()
    {
        var login = await cache.GetOrCreateAsync<string>("github-app-slug", async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheLifetime;

            var app = await appClient.GitHubApps.GetCurrent();
            return $"{app.Slug}[bot]";
        });
        return login!;
    }

    private async Task<IReadOnlyList<(string Owner, string Name, long Id)>> GetInstallationRepositoriesAsync()
    {
        return await cache.GetOrCreateAsync<IReadOnlyList<(string Owner, string Name, long Id)>>("github-app-repositories", async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheLifetime;

            var installationRepositories = await installationClient.GitHubApps.Installation.GetAllRepositoriesForCurrent(new() { PageSize = PageSize });

            var ignored = options.CurrentValue.IgnoreRepositories;

            return installationRepositories.Repositories
                .Where((p) => !p.Archived)
                .Where((p) => !p.Fork)
                .Where((p) => !ignored.Contains(p.FullName, StringComparer.OrdinalIgnoreCase))
                .Select((p) => (p.Owner.Login, p.Name, p.Id))
                .ToList();
        }) ?? [];
    }

    private async Task<PullRequestMergeMethod> GetRepositoryMergeMethodAsync(long repositoryId)
    {
        return await cache.GetOrCreateAsync<PullRequestMergeMethod>($"github-repo-{repositoryId}", async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheLifetime;

            var repository = await installationClient.Repository.Get(repositoryId);

            return PullRequestApprover.GetMergeMethod(repository);
        });
    }

    private async Task TryApproveOpenDependabotPullRequestsAsync(IssueId triggeringId)
    {
        var repositories = await GetInstallationRepositoriesAsync();

        Log.SearchingInstallationRepositories(logger, repositories.Count);

        var options = new ApiOptions() { PageSize = PageSize };
        var request = new RepositoryIssueRequest()
        {
            Creator = "app/dependabot",
            Filter = IssueFilter.Created,
            State = ItemStateFilter.Open,
        };

        await Parallel.ForEachAsync(repositories, async (repo, _) =>
        {
            var issues = await installationClient.Issue.GetAllForRepository(repo.Id, request, options);

            var issuesWithPulls = issues
                .Where((p) => p.PullRequest is { })
                .ToArray();

            var repositoryId = new RepositoryId(repo.Owner, repo.Name);

            Log.FoundDependabotPullRequests(logger, repositoryId, issuesWithPulls.Length);

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

                var appLogin = await GetAppLoginAsync();

                if (await pullRequestAnalyzer.IsApprovedByAsync(pullId, appLogin))
                {
                    Log.PullRequestAlreadyApproved(logger, pullId, appLogin);
                }
                else
                {
                    var pull = await installationClient.PullRequest.Get(repositoryId.Owner, repositoryId.Name, issue.Number);

                    bool isTrusted = await pullRequestAnalyzer.IsTrustedDependencyUpdateAsync(
                        repositoryId,
                        pull.Head.Ref,
                        pull.Head.Sha,
                        pull.Url);

                    if (isTrusted)
                    {
                        mergeMethod ??= await GetRepositoryMergeMethodAsync(repo.Id);

                        await pullRequestApprover.ApproveAndMergeAsync(
                            pullId,
                            pull.NodeId,
                            mergeMethod.GetValueOrDefault());

                        Log.PullRequestApprovedAfterImplicitTrust(logger, pullId);
                    }
                }
            }
        });
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
           Message = "Failed to approve open pull requests from Dependabot.")]
        public static partial void FailedToApproveDependabotPullRequests(
            ILogger logger,
            Exception exception);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Debug,
           Message = "Searching {Count} installation repositories for pull requests from dependabot.")]
        public static partial void SearchingInstallationRepositories(
            ILogger logger,
            int count);

        [LoggerMessage(
           EventId = 5,
           Level = LogLevel.Debug,
           Message = "Found {Count} dependabot pull requests in repository {Repository}.")]
        public static partial void FoundDependabotPullRequests(
            ILogger logger,
            RepositoryId repository,
            int count);

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
