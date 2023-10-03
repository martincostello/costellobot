// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class GitHubCommitBuilder(RepositoryBuilder repository) : ResponseBuilder
{
    public UserBuilder? Author { get; set; }

    public UserBuilder? Committer { get; set; }

    public string Message { get; set; } = RandomString();

    public IList<string> Parents { get; set; } = new List<string>();

    public RepositoryBuilder Repository { get; set; } = repository;

    public string Sha { get; set; } = RandomString();

    public override object Build()
    {
        return new
        {
            author = (Author ?? Repository.Owner).Build(),
            commit = new GitCommitBuilder(Author ?? Repository.Owner) { Message = Message }.Build(),
            parents = Parents.Select((p) => new { sha = p }).ToArray(),
            sha = Sha,
        };
    }
}
