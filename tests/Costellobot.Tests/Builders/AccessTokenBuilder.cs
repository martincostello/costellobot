// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public sealed class AccessTokenBuilder : ResponseBuilder
{
    public string Token { get; set; } = "ghs_" + RandomString();

    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow;

    public override object Build()
    {
        return new
        {
            token = Token,
            expires_at = ExpiresAt,
        };
    }
}
