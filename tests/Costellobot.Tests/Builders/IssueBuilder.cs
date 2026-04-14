// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class IssueBuilder(RepositoryBuilder repository, UserBuilder? user = null) : ResponseBuilder
{
    public string AuthorAssociation { get; set; } = "owner";

    public string HtmlUrl => $"{Repository.HtmlUrl}/issues/{Number}";

    public int Number { get; set; } = RandomNumber();

    public RepositoryBuilder Repository { get; set; } = repository;

    public PullRequestBuilder? PullRequest { get; set; }

    public string State { get; set; } = "open";

    public string Title { get; set; } = RandomString();

    public string Url => $"{Repository.Url}/issues/{Number}";

    public UserBuilder? User { get; set; } = user;

    public IssueBuilder CreatePullRequest()
    {
        PullRequest = new(Repository, User)
        {
            AuthorAssociation = AuthorAssociation,
            Number = Number,
            State = State,
            Title = Title,
        };
        return this;
    }

    public override object Build()
    {
        var user = (User ?? Repository.Owner).Build();
        return new
        {
            id = Id,
            assignees = new object[] { user },
            author_association = AuthorAssociation,
            comments_url = $"{Repository.Url}/comments{{/number}}",
            events_url = $"{Repository.Url}/events{{/privacy}}",
            html_url = HtmlUrl,
            labels_url = $"{Repository.Url}/labels{{/name}}",
            node_id = NodeId,
            number = Number,
            pull_request = PullRequest?.Build(),
            repository_url = Repository.Url,
            state = State,
            title = Title,
            url = Url,
            user,
        };
    }
}
