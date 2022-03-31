// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public class PendingDeploymentEnvironment
{
    public long Id { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string HtmlUrl { get; set; } = string.Empty;
}
