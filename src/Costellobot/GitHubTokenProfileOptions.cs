// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;

namespace MartinCostello.Costellobot;

public sealed class GitHubTokenProfileOptions
{
    public string? AppId { get; set; }

    public Dictionary<string, string> AppPermissions { get; set; } = [];

    public string? Branch { get; set; }

    public string? Environment { get; set; }

    public string? Event { get; set; }

    public IList<string> Workflows { get; set; } = [];

    public string? TokenId { get; set; }

    public bool IsAuthorized(ClaimsPrincipal user)
    {
        if (Branch is { Length: > 0 } branch)
        {
            if (user.FindFirstValue(GitHubOidcClaims.RefType) is not "branch")
            {
                return false;
            }

            if (!string.Equals(user.FindFirstValue(GitHubOidcClaims.Ref), $"refs/heads/{branch}", StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (Environment is { Length: > 0 } environment &&
            !string.Equals(user.FindFirstValue(GitHubOidcClaims.Environment), environment, StringComparison.Ordinal))
        {
            return false;
        }

        if (Event is { Length: > 0 } @event &&
            !string.Equals(user.FindFirstValue(GitHubOidcClaims.EventName), @event, StringComparison.Ordinal))
        {
            return false;
        }

        if (Workflows.Count > 0)
        {
            if (user.FindFirstValue(GitHubOidcClaims.Workflow) is { Length: > 0 } workflow &&
                !Workflows.Contains(workflow, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
