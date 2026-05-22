// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;

namespace MartinCostello.Costellobot;

public sealed class GitHubTokenProfileOptions
{
    public static readonly string Any = "*";

    public static readonly string[] AnyArray = [Any];

    public string? AppId { get; set; }

    public Dictionary<string, string> AppPermissions { get; set; } = [];

    public IList<string> Branches { get; set; } = [Any];

    public IList<string> Environments { get; set; } = [Any];

    public IList<string> Events { get; set; } = [Any];

    public IList<string> Workflows { get; set; } = [Any];

    public string? TokenId { get; set; }

    public bool IsAuthorized(ClaimsPrincipal user)
    {
        if (!Branches.SequenceEqual(AnyArray, StringComparer.Ordinal))
        {
            if (user.FindFirstValue(GitHubOidcClaims.RefType) is not "branch")
            {
                return false;
            }

            if (user.FindFirstValue(GitHubOidcClaims.Ref) is not { Length: > 0 } @ref)
            {
                return false;
            }

            if (!Branches.Any((branch) => string.Equals(@ref, $"refs/heads/{branch}", StringComparison.Ordinal)))
            {
                return false;
            }
        }

        if (!Environments.SequenceEqual(AnyArray, StringComparer.Ordinal) &&
            (user.FindFirstValue(GitHubOidcClaims.Environment) is not { Length: > 0 } environment ||
             !Environments.Contains(environment, StringComparer.Ordinal)))
        {
            return false;
        }

        if (!Events.SequenceEqual(AnyArray, StringComparer.Ordinal) &&
            (user.FindFirstValue(GitHubOidcClaims.EventName) is not { Length: > 0 } @event ||
             !Events.Contains(@event, StringComparer.Ordinal)))
        {
            return false;
        }

        if (!Workflows.SequenceEqual(AnyArray, StringComparer.Ordinal) &&
            (user.FindFirstValue(GitHubOidcClaims.Workflow) is not { Length: > 0 } workflow ||
             !Workflows.Contains(workflow, StringComparer.Ordinal)))
        {
            return false;
        }

        return true;
    }
}
