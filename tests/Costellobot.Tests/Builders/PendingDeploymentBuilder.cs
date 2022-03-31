// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class PendingDeploymentBuilder : ResponseBuilder
{
    public string Environment { get; set; } = "production";

    public override object Build()
    {
        return new
        {
            environment = new
            {
                name = Environment,
            },
            wait_timer = 0,
            wait_timer_started_at = new long?(),
            current_user_can_approve = false,
            reviewers = new[]
            {
                new
                {
                    type = "User",
                    reviewer = new
                    {
                        login = "repo-owner",
                    },
                },
            },
        };
    }
}
