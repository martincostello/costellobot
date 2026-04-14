// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class PullRequestBuilder(RepositoryBuilder repository, UserBuilder? user = null) : ResponseBuilder
{
    public string AuthorAssociation { get; set; } = "owner";

    public string CommentsUrl => $"{Repository.Url}/issues/{Number}/comments";

    public string CommitsUrl => $"{Url}/commits";

    public string Diff { get; set; } = string.Empty;

    public string DiffUrl => $"{Url}.diff";

    public string HtmlUrl => $"https://github.com/{Repository.FullName}/pull/{Number}";

    public bool IsDraft { get; set; }

    public bool? IsMergeable { get; set; }

    public string IssueUrl => $"{Repository.Url}/issues/{Number}";

    public IList<LabelBuilder> Labels { get; set; } = [];

    public int Number { get; set; } = RandomNumber();

    public RepositoryBuilder Repository { get; set; } = repository;

    public string RefBase { get; set; } = "main";

    public string RefHead { get; set; } = "dependabot/nuget/Foo-1.2.3";

    public string ReviewCommentUrl => $"{Url}/comments{{/number}}";

    public string ReviewComentsUrl => $"{Url}/comments";

    public string ShaBase { get; set; } = RandomGitSha();

    public string ShaHead { get; set; } = RandomGitSha();

    public string State { get; set; } = "open";

    public string StatusesUrl => $"{Repository.Url}/statuses/{ShaHead}";

    public string Title { get; set; } = RandomString();

    public string Url => $"{Repository.Url}/pulls/{Number}";

    public UserBuilder? User { get; set; } = user;

    public GitHubCommitBuilder CreateCommit()
        => new(Repository) { Sha = ShaHead };

    public PullRequestBuilder WithLabel(string name)
    {
        Labels.Add(new(Repository, name));
        return this;
    }

    public IssueBuilder ToIssue()
    {
        return new IssueBuilder(Repository, User)
        {
            AuthorAssociation = AuthorAssociation,
            Id = Id,
            Number = Number,
            PullRequest = this,
            State = State,
            Title = Title,
        };
    }

    public override object Build()
    {
        var repo = Repository.Build();
        var prUser = User ?? Repository.Owner;
        var user = prUser.Build();
        return new
        {
            assignees = Array.Empty<object>(),
            author_association = AuthorAssociation,
            @base = new
            {
                label = $"{prUser.Login}:{RefBase}",
                @ref = RefBase,
                sha = ShaBase,
                repo,
                user,
            },
            draft = IsDraft,
            head = new
            {
                label = $"{prUser.Login}:{RefBase}",
                @ref = RefHead,
                sha = ShaHead,
                repo,
                user,
            },
            commits_url = CommitsUrl,
            diff_url = DiffUrl,
            html_url = HtmlUrl,
            issue_url = IssueUrl,
            labels = Labels.Build(),
            mergeable = IsMergeable,
            mergeable_state = IsMergeable switch
            {
                true => "clean",
                false => "dirty",
                null => "unknown",
            },
            node_id = NodeId,
            number = Number,
            patch_url = $"{Url}.patch",
            requested_reviewers = Array.Empty<object>(),
            requested_teams = Array.Empty<object>(),
            review_comments_url = ReviewComentsUrl,
            review_comment_url = ReviewCommentUrl,
            comments_url = CommentsUrl,
            statuses_url = StatusesUrl,
            state = State,
            title = Title,
            url = Url,
            user,
            _links = new
            {
                self = new
                {
                    href = Url,
                },
                html = new
                {
                    href = HtmlUrl,
                },
                issue = new
                {
                    href = IssueUrl,
                },
                comments = new
                {
                    href = CommentsUrl,
                },
                review_comments = new
                {
                    href = ReviewComentsUrl,
                },
                review_comment = new
                {
                    href = ReviewCommentUrl,
                },
                commits = new
                {
                    href = CommitsUrl,
                },
                statuses = new
                {
                    href = StatusesUrl,
                },
            },
        };
    }
}
