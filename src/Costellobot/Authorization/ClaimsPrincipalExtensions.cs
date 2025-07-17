// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;

namespace MartinCostello.Costellobot.Authorization;

public static class ClaimsPrincipalExtensions
{
    public static bool IsAdministrator(this ClaimsPrincipal user, SiteOptions options)
        => user.IsAdministrator(options.AdminRoles, options.AdminUsers);

    public static bool IsAdministrator(this ClaimsPrincipal user, ICollection<string> roles, ICollection<string> users)
    {
        var userName = user.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(userName))
        {
            return false;
        }

        bool hasClaim = false;
        bool needsClaim = false;

        if (users is { Count: > 0 })
        {
            needsClaim = true;
            hasClaim = users.Contains(userName, StringComparer.Ordinal);
        }

        bool hasRole = false;
        bool needsRole = false;

        if (roles is { Count: > 0 })
        {
            needsRole = true;
            hasRole = roles.Any(user.IsInRole);
        }

        return (!needsClaim || hasClaim) && (!needsRole || hasRole);
    }
}
