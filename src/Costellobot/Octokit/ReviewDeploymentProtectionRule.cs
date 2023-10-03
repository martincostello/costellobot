// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public sealed class ReviewDeploymentProtectionRule(string environmentName, PendingDeploymentReviewState? state, string? comment)
{
    public string EnvironmentName { get; } = environmentName;

    public StringEnum<PendingDeploymentReviewState>? State { get; } = state;

    public string? Comment { get; } = comment;
}
