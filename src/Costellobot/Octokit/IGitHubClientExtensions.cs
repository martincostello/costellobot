// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public static class IGitHubClientExtensions
{
    public static IWorkflowRunsClient WorkflowRuns(this IGitHubClient client)
        => new WorkflowRunsClient(new ApiConnection(client.Connection));
}
