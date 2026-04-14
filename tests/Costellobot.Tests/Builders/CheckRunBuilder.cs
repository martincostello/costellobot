// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class CheckRunBuilder : ResponseBuilder
{
    public CheckRunBuilder(RepositoryBuilder repository, string status, string? conclusion)
    {
        Conclusion = conclusion;
        Repository = repository;
        Status = status;
        App = new("github-actions", repository.Owner)
        {
            Name = "GitHub Actions",
        };
    }

    public GitHubAppBuilder App { get; set; }

    public string ExternalId { get; set; } = RandomString();

    public string HeadSha { get; set; } = RandomGitSha();

    public string HtmlUrl => $"{Repository.Url}/actions/runs/{Id}/job/{Id}";

    public string Name { get; set; } = RandomString();

    public string? Conclusion { get; set; }

    public IList<PullRequestBuilder> PullRequests { get; set; } = [];

    public RepositoryBuilder Repository { get; set; }

    public string Status { get; set; }

    public override object Build()
    {
        return new
        {
            details_url = HtmlUrl,
            head_sha = HeadSha,
            html_url = HtmlUrl,
            id = Id,
            name = Name,
            node_id = NodeId,
            conclusion = Conclusion,
            external_id = ExternalId,
            pull_requests = PullRequests.Build(),
            status = Status,
            url = $"{Repository.Url}/check-runs/{Id}",
            app = App.Build(),
        };
    }
}
