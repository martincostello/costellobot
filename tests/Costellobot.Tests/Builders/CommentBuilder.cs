// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class CommentBuilder(
    IssueBuilder issue,
    UserBuilder user,
    string body,
    string authorAssociation = "OWNER") : ResponseBuilder
{
    public string AuthorAssociation { get; set; } = authorAssociation;

    public string Body { get; set; } = body;

    public IssueBuilder Issue { get; set; } = issue;

    public UserBuilder User { get; set; } = user;

    public override object Build()
    {
        return new
        {
            id = Id,
            author_association = AuthorAssociation,
            body = Body,
            html_url = $"{Issue.HtmlUrl}#issuecomment-{Id}",
            issue_url = Issue.Url,
            node_id = NodeId,
            url = $"{Issue.Repository.Url}/issues/comments/{Id}",
            user = User.Build(),
        };
    }
}
