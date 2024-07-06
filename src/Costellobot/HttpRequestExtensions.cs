// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Primitives;

namespace MartinCostello.Costellobot;

public static class HttpRequestExtensions
{
    public static bool IsJson(this HttpRequest request)
    {
        var headers = request.GetTypedHeaders();

        return IsJson(headers.ContentType?.MediaType ?? StringSegment.Empty) || headers.Accept.Any((p) => IsJson(p.MediaType));

        static bool IsJson(StringSegment? segment)
            => segment?.Equals("application/json", StringComparison.OrdinalIgnoreCase) is true;
    }
}
