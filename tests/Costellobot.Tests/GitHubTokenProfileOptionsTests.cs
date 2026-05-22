// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;

namespace MartinCostello.Costellobot;

public static class GitHubTokenProfileOptionsTests
{
    public static TheoryData<string, GitHubTokenProfileOptions, Claim[], bool> TestCases() => new()
    {
        {
            "Allows any principal when all filters use the wildcard defaults",
            new(),
            [],
            true
        },
        {
            "Rejects non-branch refs when branches are restricted",
            new()
            {
                Branches = ["main"],
            },
            [
                Claim(GitHubOidcClaims.RefType, "tag"),
                Claim(GitHubOidcClaims.Ref, "refs/tags/v1.0.0"),
            ],
            false
        },
        {
            "Rejects missing refs when branches are restricted",
            new()
            {
                Branches = ["main"],
            },
            [
                Claim(GitHubOidcClaims.RefType, "branch"),
            ],
            false
        },
        {
            "Rejects branches that are not allowed",
            new()
            {
                Branches = ["main"],
            },
            [
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Ref, "refs/heads/feature/test"),
            ],
            false
        },
        {
            "Allows matching branches",
            new()
            {
                Branches = ["main", "release"],
            },
            [
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Ref, "refs/heads/release"),
            ],
            true
        },
        {
            "Rejects missing environments when environments are restricted",
            new()
            {
                Environments = ["Production"],
            },
            [],
            false
        },
        {
            "Rejects environments that are not allowed",
            new()
            {
                Environments = ["Production"],
            },
            [
                Claim(GitHubOidcClaims.Environment, "Staging"),
            ],
            false
        },
        {
            "Allows matching environments",
            new()
            {
                Environments = ["Production", "Staging"],
            },
            [
                Claim(GitHubOidcClaims.Environment, "Production"),
            ],
            true
        },
        {
            "Rejects missing events when events are restricted",
            new()
            {
                Events = ["push"],
            },
            [],
            false
        },
        {
            "Rejects events that are not allowed",
            new()
            {
                Events = ["push"],
            },
            [
                Claim(GitHubOidcClaims.EventName, "pull_request"),
            ],
            false
        },
        {
            "Allows matching events",
            new()
            {
                Events = ["push", "workflow_dispatch"],
            },
            [
                Claim(GitHubOidcClaims.EventName, "workflow_dispatch"),
            ],
            true
        },
        {
            "Rejects missing workflows when workflows are restricted",
            new()
            {
                Workflows = ["deploy.yml"],
            },
            [],
            false
        },
        {
            "Rejects workflows that are not allowed",
            new()
            {
                Workflows = ["deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.Workflow, "build.yml"),
            ],
            false
        },
        {
            "Allows matching workflows",
            new()
            {
                Workflows = ["build.yml", "deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.Workflow, "deploy.yml"),
            ],
            true
        },
        {
            "Allows principals that satisfy every restricted filter",
            new()
            {
                Branches = ["main"],
                Environments = ["Production"],
                Events = ["workflow_dispatch"],
                Workflows = ["deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.Environment, "Production"),
                Claim(GitHubOidcClaims.EventName, "workflow_dispatch"),
                Claim(GitHubOidcClaims.Workflow, "deploy.yml"),
            ],
            true
        },
        {
            "Rejects principals when any restricted filter does not match",
            new()
            {
                Branches = ["main"],
                Environments = ["Production"],
                Events = ["workflow_dispatch"],
                Workflows = ["deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.Environment, "Production"),
                Claim(GitHubOidcClaims.EventName, "workflow_dispatch"),
                Claim(GitHubOidcClaims.Workflow, "build.yml"),
            ],
            false
        },
    };

    [Theory]
    [MemberData(nameof(TestCases))]
    public static void IsAuthorized_Returns_Expected_Result(
        string description,
        GitHubTokenProfileOptions options,
        Claim[] claims,
        bool expected)
    {
        // Arrange
        _ = description;
        var user = User(claims);

        // Act
        var actual = options.IsAuthorized(user);

        // Assert
        actual.ShouldBe(expected);
    }

    private static ClaimsPrincipal User(IEnumerable<Claim> claims)
        => new(new ClaimsIdentity(claims, "GitHub"));

    private static Claim Claim(string type, string value)
        => new(type, value);
}
