// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public sealed class WorkflowClient : IWorkflowClient
{
    private readonly IApiConnection _connection;

    public WorkflowClient(IApiConnection connection)
    {
        _connection = connection;
    }

    public async Task<WorkflowRunsResponse> GetWorkflowRunsAsync(string repositoryUrl, long checkSuiteId)
    {
        // See https://docs.github.com/en/rest/reference/actions#list-workflow-runs-for-a-repository
        var builder = new UriBuilder(repositoryUrl);
        builder.Path += "/actions/runs";
        builder.Port = -1;

        var parameters = new Dictionary<string, string>(1)
        {
            ["check_suite_id"] = checkSuiteId.ToString(CultureInfo.InvariantCulture),
        };

        return await _connection.Get<WorkflowRunsResponse>(builder.Uri, parameters);
    }

    public async Task RerunFailedJobsAsync(string repositoryUrl, long runId)
    {
        // See https://docs.github.com/en/rest/reference/actions#re-run-failed-jobs-from-a-workflow-run
        var builder = new UriBuilder(repositoryUrl);
        builder.Path += $"/actions/runs/{runId}/rerun-failed-jobs";
        builder.Port = -1;

        await _connection.Post(builder.Uri);
    }
}
