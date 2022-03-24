// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class RepositoryBuilder : ResponseBuilder
{
    public RepositoryBuilder(UserBuilder owner, string? name = null)
    {
        Name = name ?? RandomString();
        Owner = owner;
    }

    public bool AllowMergeCommit { get; set; } = true;

    public bool AllowRebaseMerge { get; set; } = true;

    public bool IsFork { get; set; }

    public bool IsPrivate { get; set; }

    public string Name { get; set; }

    public UserBuilder Owner { get; set; }

    public string? Visibility { get; set; }

    public PullRequestBuilder CreatePullRequest() => new(this);

    public override object Build()
    {
        return new
        {
            allow_merge_commit = AllowMergeCommit,
            allow_rebase_merge = AllowRebaseMerge,
            fork = IsFork,
            full_name = $"{Owner.Login}/{Name}",
            html_url = $"https://github.com/{Owner.Login}/{Name}",
            id = Id,
            name = Name,
            owner = Owner.Build(),
            @private = IsPrivate,
            visibility = Visibility,
        };
    }
}
