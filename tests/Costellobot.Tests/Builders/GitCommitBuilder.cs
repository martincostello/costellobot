// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class GitCommitBuilder(UserBuilder author) : ResponseBuilder
{
    public IList<string> Added { get; set; } = new List<string>();

    public UserBuilder Author { get; set; } = author;

    public UserBuilder? Committer { get; set; }

    public string TreeId { get; set; } = RandomString();

    public string Message { get; set; } = RandomString();

    public IList<string> Modified { get; set; } = new List<string>();

    public IList<string> Removed { get; set; } = new List<string>();

    public override object Build()
    {
        return new
        {
            id = Id.ToString(CultureInfo.InvariantCulture),
            tree_id = TreeId,
            message = Message,
            author = Author.Build(),
            committer = (Committer ?? Author).Build(),
            added = Added,
            removed = Removed,
            modified = Modified,
        };
    }
}
