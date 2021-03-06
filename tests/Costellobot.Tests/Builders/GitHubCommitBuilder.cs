// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class GitHubCommitBuilder : ResponseBuilder
{
    public GitHubCommitBuilder(RepositoryBuilder repository)
    {
        Repository = repository;
    }

    public UserBuilder? Author { get; set; }

    public string Message { get; set; } = RandomString();

    public IList<string> Parents { get; set; } = new List<string>();

    public RepositoryBuilder Repository { get; set; }

    public string Sha { get; set; } = RandomString();

    public override object Build()
    {
        return new
        {
            author = (Author ?? Repository.Owner).Build(),
            commit = new
            {
                message = Message,
            },
            parents = Parents.Select((p) => new { sha = p }).ToArray(),
            sha = Sha,
        };
    }
}
