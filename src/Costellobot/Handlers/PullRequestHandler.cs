// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using IConnection = Octokit.GraphQL.IConnection;
using PullRequestMergeMethod = Octokit.GraphQL.Model.PullRequestMergeMethod;

namespace MartinCostello.Costellobot.Handlers;

public sealed partial class PullRequestHandler : IHandler
{
    private readonly IGitHubClient _client;
    private readonly IConnection _connection;
    private readonly IOptionsMonitor<WebhookOptions> _options;
    private readonly ILogger _logger;

    public PullRequestHandler(
        IGitHubClientForInstallation client,
        IConnection connection,
        IOptionsMonitor<WebhookOptions> options,
        ILogger<PullRequestHandler> logger)
    {
        _client = client;
        _connection = connection;
        _options = options;
        _logger = logger;
    }

    public async Task HandleAsync(WebhookEvent message)
    {
        if (message is not PullRequestEvent body)
        {
            return;
        }

        if (!IsNewPullRequestFromTrustedUser(body))
        {
            Log.IgnoringPullRequestFromUntrustedUser(
                _logger,
                body.Repository?.Owner.Login,
                body.Repository?.Name,
                body.PullRequest?.Number,
                body.PullRequest?.User.Login);

            return;
        }

        string owner = body.Repository!.Owner.Login;
        string name = body.Repository.Name;
        long number = body.PullRequest!.Number;

        if (await IsTrustedDependencyUpdateAsync(body))
        {
            var options = _options.CurrentValue;

            if (options.Approve)
            {
                await ApproveAsync(owner, name, number);
            }

            if (options.Automerge)
            {
                await EnableAutoMergeAsync(
                    owner,
                    name,
                    number,
                    body.PullRequest.NodeId,
                    GetMergeMethod(body.PullRequest.Base.Repo));
            }
        }
    }

    private static PullRequestMergeMethod GetMergeMethod(Octokit.Webhooks.Models.Repository repo)
    {
        if (repo.AllowMergeCommit == true)
        {
            return PullRequestMergeMethod.Merge;
        }
        else if (repo.AllowRebaseMerge == true)
        {
            return PullRequestMergeMethod.Rebase;
        }
        else if (repo.AllowSquashMerge == true)
        {
            return PullRequestMergeMethod.Squash;
        }
        else
        {
            return PullRequestMergeMethod.Merge;
        }
    }

    private async Task ApproveAsync(
        string owner,
        string name,
        long number)
    {
        await _client.PullRequest.Review.Create(
            owner,
            name,
            (int)number,
            new()
            {
                Body = _options.CurrentValue.ApproveComment,
                Event = Octokit.PullRequestReviewEvent.Approve,
            });

        Log.PullRequestApproved(_logger, owner, name, number);
    }

    private async Task EnableAutoMergeAsync(
        string owner,
        string name,
        long number,
        string nodeId,
        PullRequestMergeMethod mergeMethod)
    {
        var input = new EnablePullRequestAutoMergeInput()
        {
            MergeMethod = mergeMethod,
            PullRequestId = new ID(nodeId),
        };

        var query = new Mutation()
            .EnablePullRequestAutoMerge(input)
            .Select((p) => new { p.PullRequest.Number })
            .Compile();

        try
        {
            await _connection.Run(query);
            Log.AutoMergeEnabled(_logger, owner, name, number);
        }
        catch (Exception ex)
        {
            Log.EnableAutoMergeFailed(_logger, ex, owner, name, number, nodeId);
        }
    }

    private bool IsNewPullRequestFromTrustedUser(PullRequestEvent message)
    {
        if (message is PullRequestEvent body &&
            body.Action == "opened" &&
            body.Repository is { } repo &&
            body.PullRequest is { } pr &&
            !pr.Draft)
        {
            return _options.CurrentValue.TrustedEntities.Users.Contains(
                pr.User.Login,
                StringComparer.Ordinal);
        }

        return false;
    }

    private async Task<bool> IsTrustedDependencyUpdateAsync(PullRequestEvent message)
    {
        var commit = await _client.Repository.Commit.Get(
            message.Repository!.Owner.Login,
            message.Repository.Name,
            message.PullRequest.Head.Sha);

        string[] commitLines = commit.Commit.Message
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var dependencies = new HashSet<string>();

        foreach (string line in commitLines)
        {
            const string Prefix = "- dependency-name: ";

            if (line.StartsWith(Prefix, StringComparison.Ordinal))
            {
                string dependencyName = line[Prefix.Length..];

                if (!string.IsNullOrEmpty(dependencyName))
                {
                    dependencies.Add(dependencyName.Trim());
                }
            }
        }

        var trustedDependencies = _options.CurrentValue.TrustedEntities.Dependencies;

        if (dependencies.Count < 1 || trustedDependencies.Count < 1)
        {
            return false;
        }

        Log.PullRequestUpdatesDependencies(
            _logger,
            message.Repository.Owner.Login,
            message.Repository.Name,
            message.PullRequest.Number,
            dependencies.ToArray());

        foreach (string dependency in dependencies)
        {
            if (!trustedDependencies.Any((p) => Regex.IsMatch(dependency, p)))
            {
                Log.UntrustedDependencyUpdated(
                    _logger,
                    message.Repository.Owner.Login,
                    message.Repository.Name,
                    message.PullRequest.Number,
                    dependency);

                return false;
            }
        }

        Log.TrustedDependenciesUpdated(
            _logger,
            message.Repository.Owner.Login,
            message.Repository.Name,
            message.PullRequest.Number,
            dependencies.Count);

        return true;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Ignoring pull request {Owner}/{Repository}#{Number} from {Login} as it is not from a trusted user.")]
        public static partial void IgnoringPullRequestFromUntrustedUser(
            ILogger logger,
            string? owner,
            string? repository,
            long? number,
            string? login);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Information,
           Message = "Pull request {Owner}/{Repository}#{Number} updates the following dependencies: {Dependencies}.")]
        public static partial void PullRequestUpdatesDependencies(
            ILogger logger,
            string owner,
            string repository,
            long number,
            string[] dependencies);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Information,
           Message = "Pull request {Owner}/{Repository}#{Number} updates dependency {Dependency} which is not trusted.")]
        public static partial void UntrustedDependencyUpdated(
            ILogger logger,
            string owner,
            string repository,
            long number,
            string dependency);

        [LoggerMessage(
           EventId = 4,
           Level = LogLevel.Information,
           Message = "Pull request {Owner}/{Repository}#{Number} updates {Count} trusted dependencies.")]
        public static partial void TrustedDependenciesUpdated(
            ILogger logger,
            string owner,
            string repository,
            long number,
            int count);

        [LoggerMessage(
           EventId = 5,
           Level = LogLevel.Information,
           Message = "Approved pull request {Owner}/{Repository}#{Number}.")]
        public static partial void PullRequestApproved(
            ILogger logger,
            string owner,
            string repository,
            long number);

        [LoggerMessage(
           EventId = 6,
           Level = LogLevel.Information,
           Message = "Enabled auto-merge for pull request {Owner}/{Repository}#{Number}.")]
        public static partial void AutoMergeEnabled(
            ILogger logger,
            string owner,
            string repository,
            long number);

        [LoggerMessage(
           EventId = 7,
           Level = LogLevel.Warning,
           Message = "Failed to enable auto-merge for pull request {Owner}/{Repository}#{Number} with node ID {NodeId}.")]
        public static partial void EnableAutoMergeFailed(
            ILogger logger,
            Exception exception,
            string owner,
            string repository,
            long number,
            string nodeId);
    }
}
