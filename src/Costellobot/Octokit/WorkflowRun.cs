// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

#pragma warning disable CA1724
public sealed class WorkflowRun
#pragma warning restore CA1724
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NodeId { get; set; } = string.Empty;

    public string HeadBranch { get; set; } = string.Empty;

    public string HeadSha { get; set; } = string.Empty;

    public int RunNumber { get; set; }

    public string @Event { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Conclusion { get; set; } = string.Empty;

    public long WorkflowId { get; set; }

    public long CheckSuiteId { get; set; }

    public int RunAttempt { get; set; }
}
