// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public sealed class PendingDeploymentReview
{
    public IList<long> EnvironmentIds { get; set; } = new List<long>();

    public string State { get; set; } = string.Empty;

    public string Comment { get; set; } = string.Empty;
}
