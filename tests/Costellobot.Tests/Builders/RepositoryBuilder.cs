// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class RepositoryBuilder(UserBuilder owner, string? name = null) : ResponseBuilder
{
    public bool? AllowMergeCommit { get; set; } = true;

    public bool? AllowRebaseMerge { get; set; } = true;

    public bool? AllowSquashMerge { get; set; } = true;

    public bool IsFork { get; set; }

    public bool IsPrivate { get; set; }

    public string Language { get; set; } = "C#";

    public string Name { get; set; } = name ?? RandomString();

    public UserBuilder Owner { get; set; } = owner;

    public GitHubCommitBuilder CreateCommit(UserBuilder? author = null)
        => new(this) { Author = author };

    public PullRequestBuilder CreatePullRequest(UserBuilder? user = null)
        => new(this, user);

    public WorkflowRunBuilder CreateWorkflowRun()
        => new(this);

    public override object Build()
    {
        return new
        {
            allow_merge_commit = AllowMergeCommit,
            allow_rebase_merge = AllowRebaseMerge,
            allow_squash_merge = AllowSquashMerge,
            fork = IsFork,
            full_name = $"{Owner.Login}/{Name}",
            html_url = $"https://github.com/{Owner.Login}/{Name}",
            id = Id,
            language = Language,
            name = Name,
            owner = Owner.Build(),
            @private = IsPrivate,
            url = $"https://api.github.com/repos/{Owner.Login}/{Name}",
        };
    }
}
