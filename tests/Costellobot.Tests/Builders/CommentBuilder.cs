// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class CommentBuilder(string body, string authorAssociation = "OWNER") : ResponseBuilder
{
    public string AuthorAssociation { get; set; } = authorAssociation;

    public string Body { get; set; } = body;

    public override object Build()
    {
        return new
        {
            id = Id,
            author_association = AuthorAssociation,
            body = Body,
        };
    }
}
