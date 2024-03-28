// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class CheckSuiteBuilder(RepositoryBuilder repository, string status, string? conclusion) : ResponseBuilder
{
    public string ApplicationName { get; set; } = "GitHub Actions";

    public string ApplicationSlug { get; set; } = "github-actions";

    public string? Conclusion { get; set; } = conclusion;

    public IList<PullRequestBuilder> PullRequests { get; set; } = [];

    public RepositoryBuilder Repository { get; set; } = repository;

    public bool Rerequestable { get; set; }

    public string Status { get; set; } = status;

    public override object Build()
    {
        return new
        {
            id = Id,
            conclusion = Conclusion,
            pull_requests = PullRequests.Build(),
            rerequestable = Rerequestable,
            status = Status,
            app = new
            {
                name = ApplicationName,
                slug = ApplicationSlug,
            },
        };
    }
}
