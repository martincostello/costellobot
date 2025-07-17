// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot.Authorization;

public sealed partial class AdministratorHandler(IOptions<SiteOptions> options) : AuthorizationHandler<AdministratorRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorRequirement requirement)
    {
        if (IsAuthorized(context.User, options.Value))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool IsAuthorized(ClaimsPrincipal user, SiteOptions options)
    {
        bool hasClaim = false;
        bool needsClaim = false;

        if (options.AdminUsers is { Count: > 0 } users)
        {
            needsClaim = true;

            foreach (var name in users)
            {
                if (user.HasClaim(ClaimTypes.Name, name))
                {
                    hasClaim = true;
                    break;
                }
            }
        }

        bool hasRole = false;
        bool needsRole = false;

        if (options.AdminRoles is { Count: > 0 } roles)
        {
            needsRole = true;

            foreach (var role in roles)
            {
                if (user.IsInRole(role))
                {
                    hasRole = true;
                    break;
                }
            }
        }

        bool authorized = false;

        if (needsClaim && needsRole)
        {
            authorized = hasClaim && hasRole;
        }
        else if (needsClaim || hasClaim)
        {
            authorized = (needsClaim && hasClaim) || (needsRole && hasRole);
        }
        else if (!needsClaim && !needsRole)
        {
            authorized = true;
        }

        return authorized;
    }
}
