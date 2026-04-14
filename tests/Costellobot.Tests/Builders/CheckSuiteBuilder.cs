// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class CheckSuiteBuilder : ResponseBuilder
{
    public CheckSuiteBuilder(RepositoryBuilder repository, string status, string? conclusion)
    {
        Conclusion = conclusion;
        Repository = repository;
        Status = status;
        App = new("github-actions", repository.Owner) { Name = "GitHub Actions" };
        HeadCommit = new(repository, repository.Owner);
    }

    public string After { get; set; } = RandomGitSha();

    public GitHubAppBuilder App { get; set; }

    public string? Conclusion { get; set; }

    public GitCommitBuilder HeadCommit { get; set; }

    public IList<PullRequestBuilder> PullRequests { get; set; } = [];

    public RepositoryBuilder Repository { get; set; }

    public bool Rerequestable { get; set; }

    public string Status { get; set; }

    public override object Build()
    {
        return new
        {
            id = Id,
            after = After,
            check_runs_url = $"{Repository.Url}/check-suites/{Id}/check-runs",
            conclusion = Conclusion,
            head_commit = HeadCommit.Build(),
            head_sha = HeadCommit.HeadSha,
            node_id = NodeId,
            pull_requests = PullRequests.Build(),
            rerequestable = Rerequestable,
            status = Status,
            url = $"{Repository.Url}/check-suites/{Id}",
            app = App.Build(),
        };
    }
}
