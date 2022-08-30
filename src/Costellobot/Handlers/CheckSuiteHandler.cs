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

public sealed partial class CheckSuiteHandler : IHandler
{
    private readonly IGitHubClient _client;
    private readonly IOptionsMonitor<WebhookOptions> _options;
    private readonly ILogger _logger;

    public CheckSuiteHandler(
        IGitHubClientForInstallation client,
        IOptionsMonitor<WebhookOptions> options,
        ILogger<CheckSuiteHandler> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task HandleAsync(WebhookEvent message)
    {
        if (message is not CheckSuiteEvent body ||
            body.Repository is null ||
            body.CheckSuite is not { } checkSuite)
        {
            return;
        }

        if (!IsCheckSuiteEligibleForRerun(body))
        {
            return;
        }

        string owner = body.Repository.Owner.Login;
        string name = body.Repository.Name;
        long checkSuiteId = checkSuite.Id;

        var pullRequest = checkSuite.PullRequests.FirstOrDefault();

        if (pullRequest is not null &&
            !await IsPullRequestFromTrustedUserAsync(owner, name, checkSuiteId, pullRequest))
        {
            return;
        }

        if (!await CanRerunCheckSuiteAsync(owner, name, checkSuiteId))
        {
            return;
        }

        // Is the check suite associated with a GitHub Actions workflow?
        var workflowsClient = _client.WorkflowRuns();

        var workflows = await workflowsClient.ListAsync(
            body.Repository.Owner.Login,
            body.Repository.Name,
            checkSuiteId);

        if (workflows.TotalCount < 1)
        {
            await RerunCheckSuiteAsync(owner, name, checkSuiteId);
        }
        else
        {
            await RerunFailedJobsAsync(
                workflowsClient,
                owner,
                name,
                workflows.WorkflowRuns[0]);
        }
    }

    private bool IsCheckSuiteEligibleForRerun(CheckSuiteEvent body)
    {
        string owner = body.Repository!.Owner.Login;
        string name = body.Repository.Name;
        var checkSuite = body.CheckSuite;
        long checkSuiteId = checkSuite.Id;

        if (!string.Equals(body.Action, CheckSuiteAction.Completed, StringComparison.Ordinal))
        {
            Log.IgnoringCheckRunAction(_logger, checkSuiteId, owner, name, body.Action);
            return false;
        }

        if (checkSuite.Conclusion != CheckSuiteConclusion.Failure)
        {
            Log.IgnoringCheckRunThatDidNotFail(_logger, checkSuiteId, owner, name);
            return false;
        }

        var options = _options.CurrentValue;

        if (options.RerunFailedChecksAttempts < 1)
        {
            Log.RetriesAreNotEnabled(_logger, checkSuiteId, owner, name);
            return false;
        }

        if (!checkSuite.Rerequestable)
        {
            Log.CannotRetryCheckSuite(_logger, checkSuiteId, owner, name);
            return false;
        }

        if (options.RerunFailedChecks.Count < 1)
        {
            Log.NoChecksConfiguredForRetry(_logger, checkSuiteId, owner, name);
            return false;
        }

        return true;
    }

    private async Task<bool> CanRerunCheckSuiteAsync(string owner, string name, long checkSuiteId)
    {
        var checkRuns = await _client.Check.Run.GetAllForCheckSuite(
            owner,
            name,
            checkSuiteId,
            new()
            {
                Filter = CheckRunCompletedAtFilter.All,
                Status = CheckStatusFilter.Completed,
            });

        var failedRuns = checkRuns.CheckRuns
            .Where((p) => p.Conclusion == CheckConclusion.Failure)
            .Where((p) => p.PullRequests.Count > 0)
            .ToList();

        if (failedRuns.Count < 1)
        {
            Log.NoFailedCheckRunsFound(_logger, checkSuiteId, owner, name);
            return false;
        }

        Log.FailedCheckRunsFound(
            _logger,
            failedRuns.Count,
            checkSuiteId,
            owner,
            name,
            failedRuns.Select((p) => p.Name).Distinct().ToArray());

        var options = _options.CurrentValue;

        var retryEligibleRuns = failedRuns
            .Where((p) => options.RerunFailedChecks.Any((pattern) => Regex.IsMatch(p.Name, pattern)))
            .GroupBy((p) => p.Name)
            .ToList();

        if (retryEligibleRuns.Count < 1)
        {
            Log.NoEligibleFailedCheckRunsFound(_logger, checkSuiteId, owner, name);
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
        var latestCheckSuites = await _client.Check.Run.GetAllForCheckSuite(owner, name, checkSuiteId);

        var latestFailingCheckRuns = latestCheckSuites.CheckRuns
            .Where((p) => retryEligibleRuns.Any((group) => group.Key == p.Name))
            .Where((p) => p.Status == CheckStatus.Completed)
            .Where((p) => p.Conclusion == CheckConclusion.Failure)
            .ToList();

        if (latestFailingCheckRuns.Count < 1)
        {
            Log.NoEligibleFailedCheckRunsFound(_logger, checkSuiteId, owner, name);
            return false;
        }

        Log.EligibileFailedCheckRunsFound(
            _logger,
            retryEligibleRuns.Count,
            checkSuiteId,
            owner,
            name,
            retryEligibleRuns.Select((p) => p.Key).ToArray());

        if (retryEligibleRuns.Any((p) => p.Count() > options.RerunFailedChecksAttempts))
        {
            Log.TooManyRetries(_logger, checkSuiteId, owner, name, options.RerunFailedChecksAttempts);
            return false;
        }

        return true;
    }

    private async Task<bool> IsPullRequestFromTrustedUserAsync(
        string owner,
        string name,
        long checkSuiteId,
        CheckRunPullRequest pull)
    {
        var connection = new ApiConnection(_client.Connection);

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
            Log.IgnoringUntrustedUser(_logger, checkSuiteId, owner, name, author.User.Login);
        }

        return isTrusted;
    }

    private async Task RerunCheckSuiteAsync(string owner, string name, long checkSuiteId)
    {
        try
        {
            await _client.Check.Suite.Rerequest(owner, name, checkSuiteId);
            Log.RerequestedCheckSuite(_logger, checkSuiteId, owner, name);
        }
        catch (Exception ex)
        {
            Log.FailedToRerequestCheckSuite(_logger, ex, checkSuiteId, owner, name);
        }
    }

    private async Task RerunFailedJobsAsync(
        IWorkflowRunsClient client,
        string owner,
        string name,
        WorkflowRun run)
    {
        try
        {
            await client.RerunFailedJobsAsync(owner, name, run.Id);

            Log.RerunningFailedJobs(_logger, run.Name, run.Id, owner, name);
        }
        catch (Exception ex)
        {
            Log.FailedToRerunFailedJobs(_logger, ex, run.Name, run.Id, owner, name);
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Ignoring check suite ID {CheckSuiteId} for {Owner}/{Repository} for action {Action}.")]
        public static partial void IgnoringCheckRunAction(
            ILogger logger,
            long? checkSuiteId,
            string? owner,
            string? repository,
            string? action);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Information,
           Message = "Ignoring check suite ID {CheckSuiteId} for {Owner}/{Repository} which did not fail.")]
        public static partial void IgnoringCheckRunThatDidNotFail(
            ILogger logger,
            long checkSuiteId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Information,
           Message = "Ignoring check suite ID {CheckSuiteId} for {Owner}/{Repository} as retries are not enabled.")]
        public static partial void RetriesAreNotEnabled(
            ILogger logger,
            long checkSuiteId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Information,
           Message = "Ignoring check suite ID {CheckSuiteId} for {Owner}/{Repository} as it cannot be retried.")]
        public static partial void CannotRetryCheckSuite(
            ILogger logger,
            long checkSuiteId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 5,
           Level = LogLevel.Information,
           Message = "Ignoring check suite ID {CheckSuiteId} for {Owner}/{Repository} as no check runs are configured to be retried.")]
        public static partial void NoChecksConfiguredForRetry(
            ILogger logger,
            long checkSuiteId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 6,
           Level = LogLevel.Information,
           Message = "No failed runs found for check suite ID {CheckSuiteId} in {Owner}/{Repository}.")]
        public static partial void NoFailedCheckRunsFound(
            ILogger logger,
            long checkSuiteId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 7,
           Level = LogLevel.Information,
           Message = "Found {Count} failed runs for check suite ID {CheckSuiteId} in {Owner}/{Repository}: {FailedCheckRuns}.")]
        public static partial void FailedCheckRunsFound(
            ILogger logger,
            int count,
            long checkSuiteId,
            string owner,
            string repository,
            string[] failedCheckRuns);

        [LoggerMessage(
           EventId = 8,
           Level = LogLevel.Information,
           Message = "No failed runs found for check suite ID {CheckSuiteId} in {Owner}/{Repository} that are eligible to be retried.")]
        public static partial void NoEligibleFailedCheckRunsFound(
            ILogger logger,
            long checkSuiteId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 9,
           Level = LogLevel.Information,
           Message = "Found {Count} total failed runs for check suite ID {CheckSuiteId} in {Owner}/{Repository} that are eligible to be retried: {EligibileCheckRuns}.")]
        public static partial void EligibileFailedCheckRunsFound(
            ILogger logger,
            int count,
            long checkSuiteId,
            string owner,
            string repository,
            string[] eligibileCheckRuns);

        [LoggerMessage(
           EventId = 10,
           Level = LogLevel.Information,
           Message = "Cannot retry check suite ID {CheckSuiteId} in {Owner}/{Repository} as there is at least one run that has been retried at least {MaximumRetries} times.")]
        public static partial void TooManyRetries(
            ILogger logger,
            long checkSuiteId,
            string owner,
            string repository,
            int maximumRetries);

        [LoggerMessage(
           EventId = 11,
           Level = LogLevel.Information,
           Message = "Check suite ID {CheckSuiteId} in {Owner}/{Repository} has been re-requested.")]
        public static partial void RerequestedCheckSuite(
            ILogger logger,
            long checkSuiteId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 12,
           Level = LogLevel.Warning,
           Message = "Failed to re-request check suite ID {CheckSuiteId} in {Owner}/{Repository}.")]
        public static partial void FailedToRerequestCheckSuite(
            ILogger logger,
            Exception exception,
            long checkSuiteId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 13,
           Level = LogLevel.Information,
           Message = "Re-running failed jobs for workflow {Name} with run ID {RunId} in {Owner}/{Repository}.")]
        public static partial void RerunningFailedJobs(
            ILogger logger,
            string name,
            long runId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 14,
           Level = LogLevel.Error,
           Message = "Failed to re-run failed jobs for workflow {Name} with run ID {RunId} in {Owner}/{Repository}.")]
        public static partial void FailedToRerunFailedJobs(
            ILogger logger,
            Exception exception,
            string name,
            long runId,
            string owner,
            string repository);

        [LoggerMessage(
           EventId = 15,
           Level = LogLevel.Information,
           Message = "Ignoring check suite ID {CheckSuiteId} for {Owner}/{Repository} as it is associated with a pull request from untrusted user {Login}.")]
        public static partial void IgnoringUntrustedUser(
            ILogger logger,
            long checkSuiteId,
            string owner,
            string repository,
            string login);
    }

    private sealed class PullRequestAuthorAssociation
    {
        public StringEnum<AuthorAssociation> AuthorAssociation { get; set; }

        public User User { get; set; } = default!;
    }
}
