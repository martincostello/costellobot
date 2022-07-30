// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class CompareResultBuilder : ResponseBuilder
{
    public IList<GitHubCommitBuilder> Commits { get; set; } = new List<GitHubCommitBuilder>();

    public string Status { get; set; } = "ahead";

    public int AheadBy { get; set; } = 1;

    public int BehindBy { get; set; }

    public override object Build()
    {
        return new
        {
            commits = Commits.Build(),
            status = Status,
            ahead_by = AheadBy,
            behind_by = BehindBy,
        };
    }
}
