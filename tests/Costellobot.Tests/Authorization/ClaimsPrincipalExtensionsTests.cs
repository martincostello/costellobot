// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;

namespace MartinCostello.Costellobot.Authorization;

public static class ClaimsPrincipalExtensionsTests
{
    public static TheoryData<string, string[], string[], string[], bool> TestCases()
        => new()
        {
            //// Any user
            { string.Empty, [], [], [], false },
            { "Alice", [], [], [], true },
            { "Bob", [], [], [], true },
            { "Alice", ["Administrator"], [], [], true },
            { "Alice", ["Administrator", "User"], [], [], true },
            { "Bob", ["User"], [], [], true },
            //// Specific users
            { "Alice", [], ["Alice"], [], true },
            { "Alice", ["Administrator"], ["Alice"], [], true },
            { "Alice", ["User"], ["Alice"], [], true },
            { "Bob", [], ["Alice"], [], false },
            { "Alice", [], ["Alice", "Bob"], [], true },
            { "Bob", [], ["Alice", "Bob"], [], true },
            { "Charlie", [], ["Alice", "Bob"], [], false },
            { "Charlie", ["Administrator"], ["Alice"], [], false },
            //// Specific roles
            { "Alice", [], [], ["Administrator"], false },
            { "Alice", ["User"], [], ["Administrator"], false },
            { "Alice", ["Administrator"], [], ["Administrator"], true },
            { "Alice", ["Owner"], [], ["Administrator", "Owner"], true },
            { "Alice", ["User"], [], ["Administrator", "Owner"], false },
            //// Specific users and roles
            { "Alice", ["Administrator"], ["Bob"], ["Administrator"], false },
            { "Alice", ["User"], ["Bob"], ["Administrator"], false },
            { "Bob", ["User"], ["Bob"], ["Administrator"], false },
            { "Alice", ["Administrator"], ["Alice", "Bob"], ["Administrator"], true },
            { "Alice", ["Owner"], ["Alice", "Bob"], ["Administrator", "Owner"], true },
            { "Bob", ["Administrator"], ["Alice", "Bob"], ["Administrator", "Owner"], true },
        };

    [Theory]
    [MemberData(nameof(TestCases))]
    public static void IsAdministrator_Returns_Correct_Result(
        string userName,
        string[] userRoles,
        string[] adminUsers,
        string[] adminRoles,
        bool expected)
    {
        // Arrange
        var claims = new List<Claim>();

        if (userName is { Length: > 0 })
        {
            claims.Add(new(ClaimTypes.Name, userName));
        }

        var identity = new ClaimsIdentity(claims);
        var user = new ClaimsPrincipal(identity);

        foreach (var role in userRoles)
        {
            identity.AddClaim(new(ClaimTypes.Role, role));
        }

        var options = new SiteOptions()
        {
            AdminRoles = adminRoles,
            AdminUsers = adminUsers,
        };

        // Act
        var actual = user.IsAdministrator(options);

        // Assert
        actual.ShouldBe(expected);
    }
}
