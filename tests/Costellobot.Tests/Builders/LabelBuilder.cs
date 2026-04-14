// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class LabelBuilder(RepositoryBuilder repo, string? name = null) : ResponseBuilder
{
    public string Name { get; set; } = name ?? RandomString();

    public RepositoryBuilder Repository { get; set; } = repo;

    public override object Build()
    {
        return new
        {
            id = Id,
            node_id = NodeId,
            url = $"{Repository.Url}/labels/{Name}",
            name = Name,
            color = "7121c6",
            @default = false,
        };
    }
}
