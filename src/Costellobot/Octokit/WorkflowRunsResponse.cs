// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public sealed class WorkflowRunsResponse
{
    public int TotalCount { get; set; }

    public IReadOnlyList<WorkflowRun> WorkflowRuns { get; set; } = Array.Empty<WorkflowRun>();
}
