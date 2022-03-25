// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Octokit;
using Terrajobst.GitHubEvents;

namespace MartinCostello.Costellobot.Handlers;

public sealed class PullRequestHandler : IHandler
{
    private readonly IGitHubClient _client;
    private readonly IOptionsSnapshot<WebhookOptions> _options;

    public PullRequestHandler(
        IGitHubClientForInstallation client,
        IOptionsSnapshot<WebhookOptions> options)
    {
        _client = client;
        _options = options;
    }

    public async Task HandleAsync(GitHubEvent message)
    {
        if (_options.Value.Comment &&
            message.Body is { } body &&
            body.Action == "opened" &&
            body.Repository is { } repo &&
            body.PullRequest is { } pr &&
            pr.User.Login == "martincostello")
        {
            await _client.Issue.Comment.Create(
                repo.Owner.Login,
                repo.Name,
                pr.Number,
                $"Hey @{pr.User.Login} :wave: - this comment is a test.");
        }
    }
}
