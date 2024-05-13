// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class IssueBuilder(RepositoryBuilder repository, UserBuilder? user = null) : ResponseBuilder
{
    public string AuthorAssociation { get; set; } = "owner";

    public int Number { get; set; } = RandomNumber();

    public RepositoryBuilder Repository { get; set; } = repository;

    public PullRequestBuilder? PullRequest { get; set; }

    public string State { get; set; } = "open";

    public string Title { get; set; } = RandomString();

    public UserBuilder? User { get; set; } = user;

    public IssueBuilder CreatePullRequest()
    {
        PullRequest = new(Repository, User)
        {
            Number = Number,
        };
        return this;
    }

    public override object Build()
    {
        return new
        {
            id = Id,
            author_association = AuthorAssociation,
            html_url = $"https://github.com/{Repository.Owner.Login}/{Repository.Name}/issues/{Number}",
            number = Number,
            pull_request = PullRequest?.Build(),
            state = State,
            title = Title,
            url = $"https://api.github.com/repos/{Repository.Owner.Login}/{Repository.Name}/issues/{Number}",
            user = (User ?? Repository.Owner).Build(),
        };
    }
}
