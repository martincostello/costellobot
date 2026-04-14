// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class PullRequestReviewBuilder(
    PullRequestBuilder pullRequest,
    UserBuilder user) : ResponseBuilder
{
    public string AuthorAssociation { get; set; } = "OWNER";

    public string Body { get; set; } = "This looks great.";

    public string HtmlUrl => $"{PullRequest.HtmlUrl}#pullrequestreview-{Id}";

    public PullRequestBuilder PullRequest { get; } = pullRequest;

    public string State { get; set; } = "commented";

    public UserBuilder User { get; set; } = user;

    public override object Build()
    {
        return new
        {
            author_association = AuthorAssociation,
            body = Body,
            commit_id = PullRequest.RefHead,
            html_url = HtmlUrl,
            id = Id,
            node_id = NodeId,
            pull_request_url = PullRequest.Url,
            state = State,
            user = User.Build(),
            _links = new
            {
                html = new
                {
                    href = HtmlUrl,
                },
                pull_request = new
                {
                    href = PullRequest.Url,
                },
            },
        };
    }
}
