// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class DeploymentStatusBuilder(
    RepositoryBuilder repository,
    UserBuilder creator,
    string state) : ResponseBuilder
{
    public UserBuilder Creator { get; set; } = creator;

    public string Description { get; set; } = RandomString();

    public string Environment { get; set; } = RandomString();

    public RepositoryBuilder Repository { get; set; } = repository;

    public string State { get; set; } = state;

    public override object Build()
    {
        return new
        {
            creator = Creator.Build(),
            deployment_url = $"{Repository.Url}/deployments/{Id}",
            description = Description,
            environment = Environment,
            id = Id,
            node_id = NodeId,
            repository_url = Repository.Url,
            state = State,
            target_url = $"{Repository.Url}/actions/runs/{Id}/job/{Id}",
            url = $"{Repository.Url}/deployments/{Id}/statuses/{Id}",
        };
    }
}
