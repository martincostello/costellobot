// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public sealed class DeploymentReviewer
{
    public string Type { get; set; } = string.Empty;

    public User Reviewer { get; set; } = default!;
}
