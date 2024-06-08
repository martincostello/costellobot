// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using RazorSlices;

namespace MartinCostello.Costellobot;

public static class RazorSliceExtensions
{
    public static string? Content(this RazorSlice slice, string? contentPath, bool appendVersion = true)
    {
        string? result = string.Empty;

        if (!string.IsNullOrEmpty(contentPath))
        {
            if (contentPath[0] == '~')
            {
                var segment = new PathString(contentPath[1..]);
                var applicationPath = slice.HttpContext!.Request.PathBase;

                var path = applicationPath.Add(segment);
                result = path.Value;
            }
            else
            {
                result = contentPath;
            }
        }

        if (appendVersion)
        {
            result += $"?v={GitMetadata.Commit}";
        }

        return result;
    }

    public static string? RouteUrl(this RazorSlice slice, string? path)
        => Content(slice, path, appendVersion: false);

    public static ClaimsPrincipal User(this RazorSlice slice)
        => slice.HttpContext!.User;
}
