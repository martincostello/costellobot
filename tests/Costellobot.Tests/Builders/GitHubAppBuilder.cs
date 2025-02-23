// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class GitHubAppBuilder(string slug, UserBuilder owner) : ResponseBuilder
{
    public string ClientId { get; set; } = RandomString();

    public string Name { get; set; } = RandomString();

    public string NodeId { get; set; } = RandomString();

    public UserBuilder Owner { get; set; } = owner;

    public string Slug { get; } = slug;

    public override object Build()
    {
        return new
        {
            client_id = ClientId,
            id = Id,
            name = Name,
            node_id = NodeId,
            owner = Owner.Build(),
            slug = Slug,
        };
    }
}
