// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public sealed class ReviewDeploymentProtectionRule
{
    public ReviewDeploymentProtectionRule(string environmentName, PendingDeploymentReviewState? state, string? comment)
    {
        EnvironmentName = environmentName;
        State = state;
        Comment = comment;
    }

    public string EnvironmentName { get; }

    public StringEnum<PendingDeploymentReviewState>? State { get; }

    public string? Comment { get; }
}
