// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public static class ResponseBuilderExtensions
{
    public static object Build<T>(this IEnumerable<T> builders)
        where T : ResponseBuilder
    {
        return builders.Select((p) => p.Build()).ToArray();
    }
}
