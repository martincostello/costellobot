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
                Log.TokenNotAuthorizedForAnyBranch(logger);
                return false;
            }

            if (user.FindFirstValue(GitHubOidcClaims.Ref) is not { Length: > 0 } @ref)
            {
                Log.TokenMissingClaim(logger, GitHubOidcClaims.Ref);
                return false;
            }

            if (!profile.Branches.Any((branch) => string.Equals(@ref, $"refs/heads/{branch}", StringComparison.Ordinal)))
            {
                Log.TokenNotAuthorizedForBranch(logger, @ref);
                return false;
            }
        }

        if (profile.Environments.Count > 0)
        {
            if (user.FindFirstValue(GitHubOidcClaims.Environment) is not { Length: > 0 } environment)
            {
                Log.TokenMissingClaim(logger, GitHubOidcClaims.Environment);
                return false;
            }

            if (!profile.Environments.Contains(environment, StringComparer.Ordinal))
            {
                Log.TokenNotAuthorizedForEnvironment(logger, environment);
                return false;
            }
        }

        if (!profile.Events.SequenceEqual(AnyArray, StringComparer.Ordinal))
        {
            if (user.FindFirstValue(GitHubOidcClaims.EventName) is not { Length: > 0 } @event)
            {
                Log.TokenMissingClaim(logger, GitHubOidcClaims.EventName);
                return false;
            }

            if (!profile.Events.Contains(@event, StringComparer.Ordinal))
            {
                Log.TokenNotAuthorizedForEvent(logger, @event);
                return false;
            }
        }

        if (user.FindFirstValue(GitHubOidcClaims.WorkflowRef) is not { Length: > 0 } workflowRef)
        {
            Log.TokenMissingClaim(logger, GitHubOidcClaims.WorkflowRef);
            return false;
        }

        if (!profile.Workflows.Any((workflow) => IsAllowedWorkflowReference(repository, workflow, profile.Branches, workflowRef)))
        {
            Log.TokenNotAuthorizedForWorkflow(logger, workflowRef);
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

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Trace,
            Message = "The GitHub OIDC token is not authorized for a branch.")]
        public static partial void TokenNotAuthorizedForAnyBranch(ILogger logger);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Trace,
            Message = "The GitHub OIDC token is missing the {ClaimId} claim.")]
        public static partial void TokenMissingClaim(ILogger logger, string claimId);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Trace,
            Message = "The GitHub OIDC token for ref {Reference} is not authorized for any configured branch.")]
        public static partial void TokenNotAuthorizedForBranch(ILogger logger, string reference);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Trace,
            Message = "The GitHub OIDC token for environment {Environment} is not authorized for any configured environment.")]
        public static partial void TokenNotAuthorizedForEnvironment(ILogger logger, string environment);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Trace,
            Message = "The GitHub OIDC token for event {EventName} is not authorized for any configured event.")]
        public static partial void TokenNotAuthorizedForEvent(ILogger logger, string eventName);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Trace,
            Message = "The GitHub OIDC token for workflow ref {WorkflowReference} is not authorized for any configured workflow.")]
        public static partial void TokenNotAuthorizedForWorkflow(ILogger logger, string workflowReference);
    }
}
