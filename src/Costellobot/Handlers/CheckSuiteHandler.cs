// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.CheckSuite;
using Octokit.Webhooks.Models.CheckSuiteEvent;
using CheckRunPullRequest = Octokit.Webhooks.Models.CheckRunEvent.CheckRunPullRequest;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class CheckSuiteHandler(
    IGitHubClientForInstallation client,
    IOptionsMonitor<WebhookOptions> options,
    ILogger<CheckSuiteHandler> logger) : IHandler
{
    private readonly IOptionsMonitor<WebhookOptions> _options = options;

    public async Task HandleAsync(WebhookEvent message)
    {
        if (message is not CheckSuiteEvent body ||
            body.Repository is null ||
            body.CheckSuite is not { } checkSuite)
        {
            return;
        }

        if (!IsCheckSuiteEligibleForRerun(body, out var repository))
        {
            return;
        }

        long checkSuiteId = checkSuite.Id;

        var pullRequest = checkSuite.PullRequests.FirstOrDefault();

        if (pullRequest is not null &&
            !await IsPullRequestFromTrustedUserAsync(repository, checkSuiteId, pullRequest))
        {
            return;
        }

        if (!await CanRerunCheckSuiteAsync(repository, checkSuiteId))
        {
            return;
        }

        // Is the check suite associated with a GitHub Actions workflow?
        var workflows = await client.Actions.Workflows.Runs.List(
            body.Repository.Owner.Login,
            body.Repository.Name,
            new() { CheckSuiteId = checkSuiteId });

        if (workflows.TotalCount < 1)
        {
            await RerunCheckSuiteAsync(repository, checkSuiteId);
        }
        else
        {
            await RerunFailedJobsAsync(repository, workflows.WorkflowRuns[0]);
        }
    }

    private bool IsCheckSuiteEligibleForRerun(
        CheckSuiteEvent body,
        out RepositoryId repository)
    {
        repository = RepositoryId.Create(body.Repository!);
        var checkSuite = body.CheckSuite;
        long checkSuiteId = checkSuite.Id;

        if (!string.Equals(body.Action, CheckSuiteAction.Completed, StringComparison.Ordinal))
        {
            Log.IgnoringCheckRunAction(logger, checkSuiteId, repository, body.Action);
            return false;
        }

        if (checkSuite.Conclusion?.Value != CheckSuiteConclusion.Failure)
        {
            Log.IgnoringCheckRunThatDidNotFail(logger, checkSuiteId, repository);
            return false;
        }

        var options = _options.CurrentValue;

        if (options.RerunFailedChecksAttempts < 1)
        {
            Log.RetriesAreNotEnabled(logger, checkSuiteId, repository);
            return false;
        }

        if (!checkSuite.Rerequestable)
        {
            Log.CannotRetryCheckSuite(logger, checkSuiteId, repository);
            return false;
        }

        if (options.RerunFailedChecks.Count < 1)
        {
            Log.NoChecksConfiguredForRetry(logger, checkSuiteId, repository);
            return false;
        }

        return true;
    }

    private async Task<bool> CanRerunCheckSuiteAsync(RepositoryId repository, long checkSuiteId)
    {
        var checkRuns = await client.Check.Run.GetAllForCheckSuite(
            repository.Owner,
            repository.Name,
            checkSuiteId,
            new()
            {
                Filter = CheckRunCompletedAtFilter.All,
                Status = CheckStatusFilter.Completed,
            });

        var failedRuns = checkRuns.CheckRuns
            .Where((p) => p.Conclusion?.Value == CheckConclusion.Failure)
            .Where((p) => p.PullRequests.Count > 0)
            .ToList();

        if (failedRuns.Count < 1)
        {
            Log.NoFailedCheckRunsFound(logger, checkSuiteId, repository);
            return false;
        }

        Log.FailedCheckRunsFound(
            logger,
            failedRuns.Count,
            checkSuiteId,
            repository,
            failedRuns.Select((p) => p.Name).Distinct().ToArray());

        var options = _options.CurrentValue;

        var retryEligibleRuns = failedRuns
            .Where((p) => options.RerunFailedChecks.Any((pattern) => Regex.IsMatch(p.Name, pattern)))
            .GroupBy((p) => p.Name)
            .ToList();

        if (retryEligibleRuns.Count < 1)
        {
            Log.NoEligibleFailedCheckRunsFound(logger, checkSuiteId, repository);
            return false;
        }

        // Get the latest status for each check unique check run that might run in the suite
        // to determine if the latest check run for the suite(s) of interest have failed or not.
        // This prevents the scenario where a workflow of "build -> publish" does the following
        // when "^build*" is configured to allow up to 2 retries:
        //   1. build is eligible for retries, and fails;
        //   2. build is retried and succeeds;
        //   3. publish runs and fails.
        // In this scenario without this check, it would be seen that the check suite failed and
        // that build only failed once, so the build would be retried even though the check run that
        // is causing the check suite to fail is the "publish" check run.
        var latestCheckSuites = await client.Check.Run.GetAllForCheckSuite(repository.Owner, repository.Name, checkSuiteId);

        var latestFailingCheckRuns = latestCheckSuites.CheckRuns
            .Where((p) => retryEligibleRuns.Any((group) => group.Key == p.Name))
            .Where((p) => p.Status.Value == CheckStatus.Completed)
            .Where((p) => p.Conclusion?.Value == CheckConclusion.Failure)
            .ToList();

        if (latestFailingCheckRuns.Count < 1)
        {
            Log.NoEligibleFailedCheckRunsFound(logger, checkSuiteId, repository);
            return false;
        }

        Log.EligibileFailedCheckRunsFound(
            logger,
            retryEligibleRuns.Count,
            checkSuiteId,
            repository,
            retryEligibleRuns.Select((p) => p.Key).ToArray());

        if (retryEligibleRuns.Any((p) => p.Count() > options.RerunFailedChecksAttempts))
        {
            Log.TooManyRetries(logger, checkSuiteId, repository, options.RerunFailedChecksAttempts);
            return false;
        }

        return true;
    }

    private async Task<bool> IsPullRequestFromTrustedUserAsync(
        RepositoryId repository,
        long checkSuiteId,
        CheckRunPullRequest pull)
    {
        var connection = new ApiConnection(client.Connection);

        var author = await connection.Get<PullRequestAuthorAssociation>(new Uri(pull.Url));

        var options = _options.CurrentValue;

        if (options.TrustedEntities.Users.Contains(author.User.Login, StringComparer.Ordinal))
        {
            return true;
        }

        bool isTrusted = author.AuthorAssociation.Value switch
        {
            AuthorAssociation.Member => true,
            AuthorAssociation.Owner => true,
            _ => false,
        };

        if (!isTrusted)
        {
            Log.IgnoringUntrustedUser(logger, checkSuiteId, repository, author.User.Login);
        }

        return isTrusted;
    }

    private async Task RerunCheckSuiteAsync(RepositoryId repository, long checkSuiteId)
    {
        try
        {
            _ = await client.Check.Suite.Rerequest(repository.Owner, repository.Name, checkSuiteId);
            Log.RerequestedCheckSuite(logger, checkSuiteId, repository);
        }
        catch (Exception ex)
        {
            Log.FailedToRerequestCheckSuite(logger, ex, checkSuiteId, repository);
        }
    }

    private async Task RerunFailedJobsAsync(
        RepositoryId repository,
        WorkflowRun run)
    {
        try
        {
            await client.Actions.Workflows.Runs.RerunFailedJobs(repository.Owner, repository.Name, run.Id);

            Log.RerunningFailedJobs(logger, run.Name, run.Id, repository);
        }
        catch (Exception ex)
        {
            Log.FailedToRerunFailedJobs(logger, ex, run.Name, run.Id, repository);
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "Ignoring check suite ID {CheckSuiteId} for {Repository} for action {Action}.")]
        public static partial void IgnoringCheckRunAction(
            ILogger logger,
            long? checkSuiteId,
            RepositoryId repository,
            string? action);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Debug,
           Message = "Ignoring check suite ID {CheckSuiteId} for {Repository} which did not fail.")]
        public static partial void IgnoringCheckRunThatDidNotFail(
            ILogger logger,
            long checkSuiteId,
            RepositoryId repository);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Debug,
           Message = "Ignoring check suite ID {CheckSuiteId} for {Repository} as retries are not enabled.")]
        public static partial void RetriesAreNotEnabled(
            ILogger logger,
            long checkSuiteId,
            RepositoryId repository);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Debug,
           Message = "Ignoring check suite ID {CheckSuiteId} for {Repository} as it cannot be retried.")]
        public static partial void CannotRetryCheckSuite(
            ILogger logger,
            long checkSuiteId,
            RepositoryId repository);

        [LoggerMessage(
           EventId = 5,
           Level = LogLevel.Debug,
           Message = "Ignoring check suite ID {CheckSuiteId} for {Repository} as no check runs are configured to be retried.")]
        public static partial void NoChecksConfiguredForRetry(
            ILogger logger,
            long checkSuiteId,
            RepositoryId repository);

        [LoggerMessage(
           EventId = 6,
           Level = LogLevel.Debug,
           Message = "No failed runs found for check suite ID {CheckSuiteId} in {Repository}.")]
        public static partial void NoFailedCheckRunsFound(
            ILogger logger,
            long checkSuiteId,
            RepositoryId repository);

        [LoggerMessage(
           EventId = 7,
           Level = LogLevel.Information,
           Message = "Found {Count} failed runs for check suite ID {CheckSuiteId} in {Repository}: {FailedCheckRuns}.")]
        public static partial void FailedCheckRunsFound(
            ILogger logger,
            int count,
            long checkSuiteId,
            RepositoryId repository,
            string[] failedCheckRuns);

        [LoggerMessage(
           EventId = 8,
           Level = LogLevel.Debug,
           Message = "No failed runs found for check suite ID {CheckSuiteId} in {Repository} that are eligible to be retried.")]
        public static partial void NoEligibleFailedCheckRunsFound(
            ILogger logger,
            long checkSuiteId,
            RepositoryId repository);

        [LoggerMessage(
           EventId = 9,
           Level = LogLevel.Information,
           Message = "Found {Count} total failed runs for check suite ID {CheckSuiteId} in {Repository} that are eligible to be retried: {EligibileCheckRuns}.")]
        public static partial void EligibileFailedCheckRunsFound(
            ILogger logger,
            int count,
            long checkSuiteId,
            RepositoryId repository,
            string[] eligibileCheckRuns);

        [LoggerMessage(
           EventId = 10,
           Level = LogLevel.Information,
           Message = "Cannot retry check suite ID {CheckSuiteId} in {Repository} as there is at least one run that has been retried at least {MaximumRetries} times.")]
        public static partial void TooManyRetries(
            ILogger logger,
            long checkSuiteId,
            RepositoryId repository,
            int maximumRetries);

        [LoggerMessage(
           EventId = 11,
           Level = LogLevel.Information,
           Message = "Check suite ID {CheckSuiteId} in {Repository} has been re-requested.")]
        public static partial void RerequestedCheckSuite(
            ILogger logger,
            long checkSuiteId,
            RepositoryId repository);

        [LoggerMessage(
           EventId = 12,
           Level = LogLevel.Warning,
           Message = "Failed to re-request check suite ID {CheckSuiteId} in {Repository}.")]
        public static partial void FailedToRerequestCheckSuite(
            ILogger logger,
            Exception exception,
            long checkSuiteId,
            RepositoryId repository);

        [LoggerMessage(
           EventId = 13,
           Level = LogLevel.Information,
           Message = "Re-running failed jobs for workflow {Name} with run ID {RunId} in {Repository}.")]
        public static partial void RerunningFailedJobs(
            ILogger logger,
            string name,
            long runId,
            RepositoryId repository);

        [LoggerMessage(
           EventId = 14,
           Level = LogLevel.Error,
           Message = "Failed to re-run failed jobs for workflow {Name} with run ID {RunId} in {Repository}.")]
        public static partial void FailedToRerunFailedJobs(
            ILogger logger,
            Exception exception,
            string name,
            long runId,
            RepositoryId repository);

        [LoggerMessage(
           EventId = 15,
           Level = LogLevel.Debug,
           Message = "Ignoring check suite ID {CheckSuiteId} for {Repository} as it is associated with a pull request from untrusted user {Login}.")]
        public static partial void IgnoringUntrustedUser(
            ILogger logger,
            long checkSuiteId,
            RepositoryId repository,
            string login);
    }

    private sealed class PullRequestAuthorAssociation
    {
        public StringEnum<AuthorAssociation> AuthorAssociation { get; set; }

        public User User { get; set; } = default!;
    }
}
