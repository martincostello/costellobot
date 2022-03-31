// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public sealed class WorkflowRunsClient : IWorkflowRunsClient
{
    private readonly IApiConnection _connection;

    public WorkflowRunsClient(IApiConnection connection)
    {
        _connection = connection;
    }

    public async Task<WorkflowRunsResponse> ListAsync(
        string owner,
        string name,
        long checkSuiteId)
    {
        // See https://docs.github.com/en/rest/reference/actions#list-workflow-runs-for-a-repository
        var uri = new Uri($"/repos/{owner}/{name}/actions/runs", UriKind.Relative);

        var parameters = new Dictionary<string, string>(1)
        {
            ["check_suite_id"] = checkSuiteId.ToString(CultureInfo.InvariantCulture),
        };

        return await _connection.Get<WorkflowRunsResponse>(uri, parameters);
    }

    public async Task RerunFailedJobsAsync(
        string owner,
        string name,
        long runId)
    {
        // See https://docs.github.com/en/rest/reference/actions#re-run-failed-jobs-from-a-workflow-run
        var uri = new Uri($"/repos/{owner}/{name}/actions/runs/{runId}/rerun-failed-jobs", UriKind.Relative);
        await _connection.Post(uri);
    }
}
