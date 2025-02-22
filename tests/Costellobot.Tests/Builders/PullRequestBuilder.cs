// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class PullRequestBuilder(RepositoryBuilder repository, UserBuilder? user = null) : ResponseBuilder
{
    public string AuthorAssociation { get; set; } = "owner";

    public string DiffUrl => $"{Url}.diff";

    public string HtmlUrl => $"https://github.com/{Repository.FullName}/pull/{Number}";

    public bool IsDraft { get; set; }

    public bool? IsMergeable { get; set; }

    public IList<LabelBuilder> Labels { get; set; } = [];

    public string NodeId { get; set; } = RandomString();

    public int Number { get; set; } = RandomNumber();

    public RepositoryBuilder Repository { get; set; } = repository;

    public string RefBase { get; set; } = "main";

    public string RefHead { get; set; } = "dependabot/nuget/Foo-1.2.3";

    public string ShaBase { get; set; } = RandomString();

    public string ShaHead { get; set; } = RandomString();

    public string State { get; set; } = "open";

    public string Title { get; set; } = RandomString();

    public string Url => $"https://api.github.com/repos/{Repository.FullName}/pulls/{Number}";

    public UserBuilder? User { get; set; } = user;

    public GitHubCommitBuilder CreateCommit()
        => new(Repository) { Sha = ShaHead };

    public PullRequestBuilder WithLabel(string name)
    {
        Labels.Add(new(name));
        return this;
    }

    public override object Build()
    {
        return new
        {
            author_association = AuthorAssociation,
            @base = new
            {
                @ref = RefBase,
                sha = ShaBase,
                repo = Repository.Build(),
            },
            draft = IsDraft,
            head = new
            {
                @ref = RefHead,
                sha = ShaHead,
            },
            diff_url = DiffUrl,
            html_url = HtmlUrl,
            labels = Labels.Build(),
            mergeable = IsMergeable,
            node_id = NodeId,
            number = Number,
            state = State,
            title = Title,
            url = Url,
            user = (User ?? Repository.Owner).Build(),
        };
    }
}
