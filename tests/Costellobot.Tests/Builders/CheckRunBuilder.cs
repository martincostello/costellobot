// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class CheckRunBuilder : ResponseBuilder
{
    public CheckRunBuilder(string status, string? conclusion)
    {
        Status = status;
        Conclusion = conclusion;
    }

    public string ApplicationName { get; set; } = "GitHub Actions";

    public string Name { get; set; } = RandomString();

    public string? Conclusion { get; set; }

    public IList<PullRequestBuilder> PullRequests { get; set; } = new List<PullRequestBuilder>();

    public string Status { get; set; }

    public override object Build()
    {
        return new
        {
            id = Id,
            name = Name,
            conclusion = Conclusion,
            pull_requests = PullRequests.Build(),
            status = Status,
            app = new
            {
                name = ApplicationName,
            },
        };
    }
}
