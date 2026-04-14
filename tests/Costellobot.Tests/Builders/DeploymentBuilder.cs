// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class DeploymentBuilder(RepositoryBuilder repository, UserBuilder creator) : ResponseBuilder
{
    public UserBuilder Creator { get; set; } = creator;

    public string Environment { get; set; } = RandomString();

    public string Ref { get; set; } = RandomGitSha();

    public RepositoryBuilder Repository { get; set; } = repository;

    public string Sha { get; set; } = RandomGitSha();

    public string Task { get; set; } = "deploy";

    public PendingDeploymentBuilder CreatePendingDeployment()
        => new() { Environment = Environment };

    public override object Build()
    {
        return new
        {
            id = Id,
            creator = Creator.Build(),
            environment = Environment,
            node_id = NodeId,
            original_environment = Environment,
            @ref = Ref,
            repository_url = Repository.Url,
            sha = Sha,
            statuses_url = $"{Repository.Url}/deployments/{Id}/statuses",
            task = Task,
            url = $"{Repository.Url}/deployments/{Id}",
        };
    }
}
