// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class WorkflowRunBuilder : ResponseBuilder
{
    public WorkflowRunBuilder(RepositoryBuilder repository)
    {
        Repository = repository;
    }

    public string Name { get; set; } = RandomString();

    public RepositoryBuilder Repository { get; set; }

    public override object Build()
    {
        return new
        {
            id = Id,
            name = Name,
        };
    }
}
