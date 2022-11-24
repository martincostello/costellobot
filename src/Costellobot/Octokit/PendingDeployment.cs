// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public sealed class PendingDeployment
{
    public PendingDeploymentEnvironment Environment { get; set; } = default!;

    public long WaitTimer { get; set; }

    public DateTimeOffset? WaitTimerStartedAt { get; set; }

    public bool CurrentUserCanApprove { get; set; }
}
