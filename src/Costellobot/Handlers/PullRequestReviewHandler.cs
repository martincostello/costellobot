// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Octokit.Webhooks;
using Octokit.Webhooks.Events.PullRequestReview;
using Octokit.Webhooks.Models.PullRequestReviewEvent;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class PullRequestReviewHandler(
    PullRequestAnalyzer pullRequestAnalyzer,
    ITrustStore trustStore,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<PullRequestReviewHandler> logger) : IHandler
{
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

        if (await pullRequestAnalyzer.HasValidApprovalAsync(IssueId.Create(repo, pr.Number), pr.Draft))
        {
            // Already approved
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

        if (trustAdditions is 1)
        {
            // TODO Approve any open and unapproved PRs that also update it
        }

        static bool IsApprovedByOwner(Review review) =>
            review?.AuthorAssociation.Value == Octokit.Webhooks.Models.AuthorAssociation.Owner &&
            review?.State?.Value == Octokit.Webhooks.Models.PullRequestReviewEvent.ReviewState.Approved;
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
    }
}
