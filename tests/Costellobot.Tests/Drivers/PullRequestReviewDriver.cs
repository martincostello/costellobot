// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Builders;

using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Drivers;

public sealed class PullRequestReviewDriver : PullRequestDriver
{
    public PullRequestReviewDriver(string? reviewerLogin = null, string? authorLogin = null)
        : base(authorLogin)
    {
        Review = new(PullRequest, CreateUser(reviewerLogin));
        Sender = Review.User;
    }

    public PullRequestReviewBuilder Review { get; set; }

    public static PullRequestReviewDriver FromUserForDependabot(string login = "martincostello")
        => new(login, DependabotCommitter);

    public PullRequestReviewDriver WithAuthorAssociation(string value)
    {
        Review.AuthorAssociation = value;
        return this;
    }

    public PullRequestReviewDriver WithState(string value)
    {
        Review.State = value;
        return this;
    }

    public override object CreateWebhook(string action)
    {
        return new
        {
            action,
            review = Review.Build(),
            pull_request = PullRequest.Build(),
            repository = PullRequest.Repository.Build(),
            sender = Sender.Build(),
            installation = new
            {
                id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
            },
        };
    }
}
