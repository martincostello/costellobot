// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public interface IWorkflowRunsClient
{
    Task<WorkflowRunsResponse> ListAsync(
        string owner,
        string name,
        long checkSuiteId);

    Task RerunFailedJobsAsync(
        string owner,
        string name,
        long runId);
}
