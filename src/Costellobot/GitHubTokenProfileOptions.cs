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

    public IList<string> Branches { get; set; } = [];

    public IList<string> Environments { get; set; } = [];

    public IList<string> Events { get; set; } = [];

    public IList<string> Workflows { get; set; } = [];

    public string? TokenId { get; set; }

    public bool IsAuthorized(ClaimsPrincipal user, string repository)
    {
        if (Branches.Count < 1 || Events.Count < 1 || Workflows.Count < 1)
        {
            return false;
        }

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

        if (Environments.Count > 0 &&
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

        if (user.FindFirstValue(GitHubOidcClaims.WorkflowRef) is not { Length: > 0 } workflowRef)
        {
            return false;
        }

        if (!Workflows.Any((workflow) => IsAllowedWorkflowReference(repository, workflow, Branches, workflowRef)))
        {
            return false;
        }

        return true;
    }

    private static bool IsAllowedWorkflowReference(
        string repository,
        string allowedWorkflow,
        IList<string> allowedBranches,
        string workflowReference)
    {
        if (allowedBranches.SequenceEqual(AnyArray, StringComparer.Ordinal))
        {
            return workflowReference.StartsWith($"{repository}/.github/workflows/{allowedWorkflow}@refs/heads/", StringComparison.Ordinal);
        }

        foreach (var branch in allowedBranches)
        {
            if (string.Equals(workflowReference, $"{repository}/.github/workflows/{allowedWorkflow}@refs/heads/{branch}", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
