// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;

namespace MartinCostello.Costellobot;

public sealed partial class GitHubTokenProfileAuthorizer(ILogger<GitHubTokenProfileAuthorizer> logger)
{
    public static readonly string Any = "*";

    public static readonly string[] AnyArray = [Any];

    public bool IsAuthorized(
        ClaimsPrincipal user,
        GitHubTokenProfileOptions profile,
        string repository)
    {
        if (profile.Branches.Count < 1 || profile.Events.Count < 1 || profile.Workflows.Count < 1)
        {
            Log.InvalidProfileConfiguration(logger);
            return false;
        }

        if (!profile.Branches.SequenceEqual(AnyArray, StringComparer.Ordinal))
        {
            if (user.FindFirstValue(GitHubOidcClaims.RefType) is not "branch")
            {
                return false;
            }

            if (user.FindFirstValue(GitHubOidcClaims.Ref) is not { Length: > 0 } @ref)
            {
                return false;
            }

            if (!profile.Branches.Any((branch) => string.Equals(@ref, $"refs/heads/{branch}", StringComparison.Ordinal)))
            {
                return false;
            }
        }

        if (profile.Environments.Count > 0 &&
            (user.FindFirstValue(GitHubOidcClaims.Environment) is not { Length: > 0 } environment ||
             !profile.Environments.Contains(environment, StringComparer.Ordinal)))
        {
            return false;
        }

        if (!profile.Events.SequenceEqual(AnyArray, StringComparer.Ordinal) &&
            (user.FindFirstValue(GitHubOidcClaims.EventName) is not { Length: > 0 } @event ||
             !profile.Events.Contains(@event, StringComparer.Ordinal)))
        {
            return false;
        }

        if (user.FindFirstValue(GitHubOidcClaims.WorkflowRef) is not { Length: > 0 } workflowRef)
        {
            return false;
        }

        if (!profile.Workflows.Any((workflow) => IsAllowedWorkflowReference(repository, workflow, profile.Branches, workflowRef)))
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

        return allowedBranches.Any((branch) => string.Equals(workflowReference, $"{repository}/.github/workflows/{allowedWorkflow}@refs/heads/{branch}", StringComparison.Ordinal));
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Trace,
            Message = "The GitHub token profile configuration is invalid.")]
        public static partial void InvalidProfileConfiguration(ILogger logger);
    }
}
