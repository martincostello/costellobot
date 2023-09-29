// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public sealed class WorkflowRunsClient(IApiConnection connection) : IWorkflowRunsClient
{
    public async Task<IReadOnlyList<PendingDeployment>> GetPendingDeploymentsAsync(
        string owner,
        string name,
        long runId)
    {
        // See https://docs.github.com/en/rest/reference/actions#get-pending-deployments-for-a-workflow-run
        var uri = new Uri($"repos/{owner}/{name}/actions/runs/{runId}/pending_deployments", UriKind.Relative);
        return await connection.GetAll<PendingDeployment>(uri);
    }

    public async Task ReviewCustomProtectionRuleAsync(
        string deploymentCallbackUrl,
        ReviewDeploymentProtectionRule review)
    {
        // See https://docs.github.com/en/rest/actions/workflow-runs?apiVersion=2022-11-28#review-custom-deployment-protection-rules-for-a-workflow-run
        var uri = new Uri(deploymentCallbackUrl, UriKind.Absolute);
        var status = await connection.Connection.Post(uri, review, "application/vnd.github+json");

        if (status is not System.Net.HttpStatusCode.NoContent)
        {
            throw new ApiException("Failed to review custom deployment protection rule.", status);
        }
    }
}
