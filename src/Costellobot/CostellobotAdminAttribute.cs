// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Authorization;

namespace MartinCostello.Costellobot;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class CostellobotAdminAttribute() : AuthorizeAttribute(AuthenticationEndpoints.AdminPolicyName)
{
}
