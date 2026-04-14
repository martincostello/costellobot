// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class WorkflowRunBuilder(RepositoryBuilder repository) : ResponseBuilder
{
    public string CheckSuiteNodeId { get; set; } = RandomString();

    public string Event { get; set; } = "push";

    public string HeadBranch { get; set; } = repository.DefaultBranch;

    public string HeadSha { get; set; } = RandomGitSha();

    public string Name { get; set; } = RandomString();

    public RepositoryBuilder Repository { get; set; } = repository;

    public override object Build()
    {
        var actor = Repository.Owner.Build();

        return new
        {
            actor,
            check_suite_node_id = CheckSuiteNodeId,
            @event = Event,
            head_branch = HeadBranch,
            head_sha = HeadSha,
            html_url = $"{Repository.Url}/actions/runs/{Id}",
            id = Id,
            name = Name,
            node_id = NodeId,
            pull_requests = Array.Empty<object>(),
            triggering_actor = actor,
            url = $"{Repository.Url}/actions/runs/{Id}",
        };
    }
}
