﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot.Authorization;

/// <summary>
/// A class representing an authorization handler for site administrators.
/// </summary>
/// <param name="options">The <see cref="SiteOptions"/> to use.</param>
public sealed partial class AdministratorHandler(IOptionsMonitor<SiteOptions> options) : AuthorizationHandler<AdministratorRequirement>
{
    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorRequirement requirement)
    {
        if (context.User.IsAdministrator(options.CurrentValue))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
