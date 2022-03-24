// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class IssueCommentBuilder : ResponseBuilder
{
    public IssueCommentBuilder(UserBuilder user)
    {
        User = user;
    }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Body { get; set; } = RandomString();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public UserBuilder User { get; set; }

    public override object Build()
    {
        return new
        {
            body = Body,
            created_at = CreatedAt.ToString("o", CultureInfo.InvariantCulture),
            id = Id,
            user = User.Build(),
            updated_at = UpdatedAt.ToString("o", CultureInfo.InvariantCulture),
        };
    }
}
