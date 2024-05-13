// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Builders;

using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Drivers;

public sealed class IssueCommentDriver
{
    public IssueCommentDriver(string body, string authorAssociation = "OWNER")
    {
        Owner = Sender = User = CreateUser();
        Repository = Owner.CreateRepository();
        Issue = Repository.CreateIssue(User);
        Comment = new CommentBuilder(body, authorAssociation);
    }

    public CommentBuilder Comment { get; set; }

    public IssueBuilder Issue { get; set; }

    public UserBuilder Owner { get; set; }

    public RepositoryBuilder Repository { get; set; }

    public UserBuilder Sender { get; set; }

    public UserBuilder User { get; set; }

    public object CreateWebhook(string action)
    {
        return new
        {
            action,
            comment = Comment.Build(),
            installation = new
            {
                id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
            },
            issue = Issue.Build(),
            repository = Repository.Build(),
            sender = Sender.Build(),
        };
    }
}
