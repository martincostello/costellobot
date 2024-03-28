// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class CheckRunBuilder(string status, string? conclusion) : ResponseBuilder
{
    public string ApplicationName { get; set; } = "GitHub Actions";

    public string Name { get; set; } = RandomString();

    public string? Conclusion { get; set; } = conclusion;

    public IList<PullRequestBuilder> PullRequests { get; set; } = [];

    public string Status { get; set; } = status;

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
