// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class GitCommitBuilder(RepositoryBuilder repository, UserBuilder author) : ResponseBuilder
{
    public IList<string> Added { get; set; } = [];

    public UserBuilder Author { get; set; } = author;

    public UserBuilder? Committer { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public string TreeId { get; set; } = RandomGitSha();

    public string Message { get; set; } = RandomString();

    public IList<string> Modified { get; set; } = [];

    public IList<string> Removed { get; set; } = [];

    public RepositoryBuilder Repository { get; set; } = repository;

    public override object Build()
    {
        return new
        {
            id = Id.ToString(CultureInfo.InvariantCulture),
            timestamp = Timestamp.ToString("u", CultureInfo.InvariantCulture),
            tree_id = TreeId,
            message = Message,
            author = Author.Build(),
            committer = (Committer ?? Author).Build(),
            added = Added,
            removed = Removed,
            modified = Modified,
            url = $"{Repository.HtmlUrl}/commit/{Id}",
        };
    }
}
