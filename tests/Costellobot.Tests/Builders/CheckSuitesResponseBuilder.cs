// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class CheckSuitesResponseBuilder : ResponseBuilder
{
    public IList<CheckSuiteBuilder> CheckSuites { get; } = new List<CheckSuiteBuilder>();

    public override object Build()
    {
        return new
        {
            check_suites = CheckSuites.Build(),
            total_count = CheckSuites.Count,
        };
    }
}
