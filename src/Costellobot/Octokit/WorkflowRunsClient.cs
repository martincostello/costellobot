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

    public async Task<IReadOnlyList<PendingDeployment>> GetPendingDeploymentsAsync(
        string owner,
        string name,
        long runId)
    {
        // See https://docs.github.com/en/rest/reference/actions#get-pending-deployments-for-a-workflow-run
        var uri = new Uri($"repos/{owner}/{name}/actions/runs/{runId}/pending_deployments", UriKind.Relative);
        return await _connection.GetAll<PendingDeployment>(uri);
    }
}
