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
            new()
            {
                Branches = ["*"],
                Events = ["*"],
                Workflows = ["ci.yml"],
            },
            [
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/ci.yml@refs/heads/main"),
            ],
            true
        },
        {
            "Rejects non-branch refs when branches are restricted",
            new()
            {
                Branches = ["main"],
                Events = ["*"],
                Workflows = ["ci.yml"],
            },
            [
                Claim(GitHubOidcClaims.Ref, "refs/tags/v1.0.0"),
                Claim(GitHubOidcClaims.RefType, "tag"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/ci.yml@refs/tags/v1.0.0"),
            ],
            false
        },
        {
            "Rejects missing refs when branches are restricted",
            new()
            {
                Branches = ["main"],
                Events = ["*"],
                Workflows = ["ci.yml"],
            },
            [
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/ci.yml@refs/heads/main"),
            ],
            false
        },
        {
            "Rejects branches that are not allowed",
            new()
            {
                Branches = ["main"],
                Events = ["*"],
                Workflows = ["ci.yml"],
            },
            [
                Claim(GitHubOidcClaims.Ref, "refs/heads/feature/test"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/ci.yml@refs/heads/feature/test"),
            ],
            false
        },
        {
            "Allows matching branches",
            new()
            {
                Branches = ["main", "release"],
                Events = ["*"],
                Workflows = ["ci.yml"],
            },
            [
                Claim(GitHubOidcClaims.Ref, "refs/heads/release"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/ci.yml@refs/heads/release"),
            ],
            true
        },
        {
            "Rejects missing environments when environments are restricted",
            new()
            {
                Branches = ["*"],
                Environments = ["Production"],
                Events = ["*"],
                Workflows = ["ci.yml"],
            },
            [
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/ci.yml@refs/heads/main"),
            ],
            false
        },
        {
            "Rejects environments that are not allowed",
            new()
            {
                Branches = ["*"],
                Environments = ["Production"],
                Events = ["*"],
                Workflows = ["deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.Environment, "Staging"),
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/deploy.yml@refs/heads/main"),
            ],
            false
        },
        {
            "Allows matching environments",
            new()
            {
                Branches = ["*"],
                Environments = ["Production", "Staging"],
                Events = ["*"],
                Workflows = ["deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.Environment, "Production"),
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/deploy.yml@refs/heads/main"),
            ],
            true
        },
        {
            "Rejects missing events when events are restricted",
            new()
            {
                Branches = ["*"],
                Events = ["push"],
                Workflows = ["deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/deploy.yml@refs/heads/main"),
            ],
            false
        },
        {
            "Rejects events that are not allowed",
            new()
            {
                Branches = ["*"],
                Events = ["push"],
                Workflows = ["deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.EventName, "pull_request"),
                Claim(GitHubOidcClaims.Ref, "refs/heads/some-branch"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/deploy.yml@refs/heads/main"),
            ],
            false
        },
        {
            "Allows matching events",
            new()
            {
                Branches = ["*"],
                Events = ["push", "workflow_dispatch"],
                Workflows = ["deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.EventName, "workflow_dispatch"),
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/deploy.yml@refs/heads/main"),
            ],
            true
        },
        {
            "Rejects missing workflows when workflows are restricted",
            new()
            {
                Branches = ["*"],
                Events = ["*"],
                Workflows = ["deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
            ],
            false
        },
        {
            "Rejects when no workflows are configured",
            new()
            {
                Branches = ["*"],
                Events = ["*"],
                Workflows = [],
            },
            [
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/deploy.yml@refs/heads/main"),
            ],
            false
        },
        {
            "Rejects wildcard workflows",
            new()
            {
                Branches = ["*"],
                Events = ["*"],
                Workflows = ["*"],
            },
            [
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/deploy.yml@refs/heads/main"),
            ],
            false
        },
        {
            "Rejects workflows that are not allowed",
            new()
            {
                Branches = ["*"],
                Events = ["*"],
                Workflows = ["deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/build.yml@refs/heads/main"),
            ],
            false
        },
        {
            "Rejects workflows refs that are not allowed",
            new()
            {
                Branches = ["main"],
                Events = ["*"],
                Workflows = ["deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/build.yml@refs/heads/feature-branch"),
            ],
            false
        },
        {
            "Allows matching workflows",
            new()
            {
                Branches = ["*"],
                Events = ["*"],
                Workflows = ["build.yml", "deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/deploy.yml@refs/heads/main"),
            ],
            true
        },
        {
            "Allows matching workflows with matching branch",
            new()
            {
                Branches = ["deploy"],
                Events = ["*"],
                Workflows = ["build.yml", "deploy.yml"],
            },
            [
                Claim(GitHubOidcClaims.Ref, "refs/heads/deploy"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/deploy.yml@refs/heads/deploy"),
            ],
            true
        },
        {
            "Allows principals that satisfy every restricted filter",
            new()
            {
                Branches = ["release"],
                Environments = ["Production"],
                Events = ["workflow_dispatch"],
                Workflows = ["deploy.yaml"],
            },
            [
                Claim(GitHubOidcClaims.Environment, "Production"),
                Claim(GitHubOidcClaims.EventName, "workflow_dispatch"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Ref, "refs/heads/release"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/deploy.yaml@refs/heads/release"),
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
                Claim(GitHubOidcClaims.Environment, "Production"),
                Claim(GitHubOidcClaims.EventName, "workflow_dispatch"),
                Claim(GitHubOidcClaims.Repository, "martincostello/costellobot"),
                Claim(GitHubOidcClaims.RefType, "branch"),
                Claim(GitHubOidcClaims.Ref, "refs/heads/main"),
                Claim(GitHubOidcClaims.WorkflowRef, "martincostello/costellobot/.github/workflows/deploy.yml@refs/heads/feature"),
            ],
            false
        },
    };

    [Theory]
#pragma warning disable xUnit1044
#pragma warning disable xUnit1045
    [MemberData(nameof(TestCases))]
#pragma warning restore xUnit1045
#pragma warning restore xUnit1044
    public static void IsAuthorized_Returns_Expected_Result(
        string description,
        GitHubTokenProfileOptions options,
        Claim[] claims,
        bool expected)
    {
        // Arrange
        _ = description;

        var user = User(claims);
        var repository = user.FindFirstValue(GitHubOidcClaims.Repository);

        // Act
        var actual = options.IsAuthorized(user, repository ?? string.Empty);

        // Assert
        actual.ShouldBe(expected);
    }

    private static ClaimsPrincipal User(IEnumerable<Claim> claims)
        => new(new ClaimsIdentity(claims, "GitHub"));

    private static Claim Claim(string type, string value)
        => new(type, value);
}
