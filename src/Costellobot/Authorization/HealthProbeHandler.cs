// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;

namespace MartinCostello.Costellobot.Authorization;

/// <summary>
/// A class representing an authorization handler for automated health probes.
/// </summary>
/// <param name="configuration">The <see cref="IConfiguration"/> to use.</param>
public sealed partial class HealthProbeHandler(IConfiguration configuration) : AuthorizationHandler<HealthProbeRequirement>
{
    private readonly string? _encryptionKeyHash = GetEncryptionKeyHash(configuration);

    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, HealthProbeRequirement requirement)
    {
        if (_encryptionKeyHash is { Length: > 0 } && context.Resource is HttpContext httpContext)
        {
            // See https://learn.microsoft.com/azure/app-service/monitor-instances-health-check?tabs=dotnet#authentication-and-security
            const string HealthProbeTokenKey = "x-health-probe-token";

            var token =
                httpContext.Request.Headers["x-ms-auth-internal-token"].FirstOrDefault() ??
                httpContext.Request.Headers[HealthProbeTokenKey].FirstOrDefault() ??
                httpContext.Request.Query[HealthProbeTokenKey].FirstOrDefault();

            if (token is { } && FixedTimeEquals(token, _encryptionKeyHash))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }

    private static string? GetEncryptionKeyHash(IConfiguration configuration)
    {
        var key = configuration["WEBSITE_AUTH_ENCRYPTION_KEY"];

        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
        {
            return null;
        }

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var hashBytes = SHA256.HashData(keyBytes);

        return Convert.ToBase64String(hashBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool FixedTimeEquals(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        int length = left.Length;
        int accumulator = 0;

        for (int i = 0; i < length; i++)
        {
            accumulator |= left[i] - right[i];
        }

        return accumulator == 0;
    }
}
