// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class PullRequestBuilder : ResponseBuilder
{
    public PullRequestBuilder(RepositoryBuilder repository, UserBuilder? user = null)
    {
        Repository = repository;
        User = user;
    }

    public bool IsDraft { get; set; }

    public bool? IsMergeable { get; set; }

    public string NodeId { get; set; } = RandomString();

    public int Number { get; set; } = RandomNumber();

    public RepositoryBuilder Repository { get; set; }

    public string Sha { get; set; } = RandomString();

    public string Title { get; set; } = RandomString();

    public UserBuilder? User { get; set; }

    public GitHubCommitBuilder CreateCommit()
        => new(Repository) { Sha = Sha };

    public override object Build()
    {
        return new
        {
            draft = IsDraft,
            head = new
            {
                sha = Sha,
            },
            html_url = $"https://github.com/{Repository.Owner.Login}/{Repository.Name}/pull/{Number}",
            mergeable = IsMergeable,
            node_id = NodeId,
            number = Number,
            title = Title,
            user = (User ?? Repository.Owner).Build(),
        };
    }
}
