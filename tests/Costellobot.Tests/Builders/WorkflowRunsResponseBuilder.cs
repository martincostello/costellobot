// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class WorkflowRunsResponseBuilder : ResponseBuilder
{
    public IList<WorkflowRunBuilder> WorkflowRuns { get; } = new List<WorkflowRunBuilder>();

    public override object Build()
    {
        return new
        {
            total_count = WorkflowRuns.Count,
            workflow_runs = WorkflowRuns.Build(),
        };
    }
}
