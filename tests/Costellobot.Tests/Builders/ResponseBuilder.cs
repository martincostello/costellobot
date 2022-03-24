// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace MartinCostello.Costellobot.Builders;

public abstract class ResponseBuilder
{
    public int Id { get; set; } = RandomNumber();

    public abstract object Build();

    protected static int RandomNumber() => RandomNumberGenerator.GetInt32(int.MaxValue);

    protected static string RandomString() => Guid.NewGuid().ToString();
}
