// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class DeploymentBuilder : ResponseBuilder
{
    public string Environment { get; set; } = RandomString();

    public string Sha { get; set; } = RandomString();

    public PendingDeploymentBuilder CreatePendingDeployment()
        => new() { Environment = Environment };

    public override object Build()
    {
        return new
        {
            id = Id,
            environment = Environment,
            sha = Sha,
        };
    }
}
