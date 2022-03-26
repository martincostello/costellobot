// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.CheckSuite;
using Octokit.Webhooks.Models.CheckSuiteEvent;

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

        string owner = body.Repository.Owner.Login;
        string name = body.Repository.Name;
        long checkSuiteId = checkSuite.Id;

        if (!string.Equals(body.Action, CheckSuiteAction.Completed, StringComparison.Ordinal))
        {
            Log.IgnoringCheckRunAction(_logger, checkSuiteId, owner, name, body.Action);
            return;
        }

        if (checkSuite.Conclusion != CheckSuiteConclusion.Failure)
        {
            Log.IgnoringCheckRunThatDidNotFail(_logger, checkSuiteId, owner, name);
            return;
        }

        var options = _options.CurrentValue;

        if (options.RerunFailedChecksAttempts < 1)
        {
            Log.RetriesAreNotEnabled(_logger, checkSuiteId, owner, name);
            return;
        }

        if (!checkSuite.Rerequestable)
        {
            Log.CannotRetryCheckSuite(_logger, checkSuiteId, owner, name);
            return;
        }

        if (options.RerunFailedChecks.Count < 1)
        {
            Log.NoChecksConfiguredForRetry(_logger, checkSuiteId, owner, name);
            return;
        }

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
            return;
        }

        Log.FailedCheckRunsFound(
            _logger,
            failedRuns.Count,
            checkSuiteId,
            owner,
            name,
            failedRuns.Select((p) => p.Name).Distinct().ToArray());

        var retryEligibleRuns = failedRuns
            .Where((p) => options.RerunFailedChecks.Any((pattern) => Regex.IsMatch(p.Name, pattern)))
            .GroupBy((p) => p.Name)
            .ToList();

        if (retryEligibleRuns.Count < 1)
        {
            Log.NoEligibleFailedCheckRunsFound(_logger, checkSuiteId, owner, name);
            return;
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
            return;
        }

        var client = _client.Workflows();

        // Is the check suite associated with a GitHub Actions workflow?
        var workflows = await client.GetWorkflowRunsAsync(body.Repository.Url, checkSuiteId);

        if (workflows.TotalCount < 1)
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
        else
        {
            var run = workflows.WorkflowRuns[0];

            try
            {
                await client.RerunFailedJobsAsync(body.Repository.Url, run.Id);

                Log.RerunningFailedJobs(_logger, run.Name, run.Id, owner, name);
            }
            catch (Exception ex)
            {
                Log.FailedToRerunFailedJobs(_logger, ex, run.Name, run.Id, owner, name);
            }
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
    }
}
