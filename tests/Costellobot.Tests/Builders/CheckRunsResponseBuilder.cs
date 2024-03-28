// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class CheckRunsResponseBuilder : ResponseBuilder
{
    public IList<CheckRunBuilder> CheckRuns { get; } = [];

    public override object Build()
    {
        return new
        {
            check_runs = CheckRuns.Build(),
            total_count = CheckRuns.Count,
        };
    }
}
