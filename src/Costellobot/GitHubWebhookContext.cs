// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Octokit;
using IConnection = Octokit.GraphQL.IConnection;

namespace MartinCostello.Costellobot;

public sealed class GitHubWebhookContext(
    IGitHubClientFactory clientFactory,
    IOptionsMonitor<GitHubOptions> githubOptions,
    IOptionsMonitor<WebhookOptions> webhookOptions)
{
    private IGitHubClientForApp? _appClient;
    private IConnection? _graphQLClient;
    private IGitHubClientForInstallation? _installationClient;
    private IGitHubClientForUser? _userClient;

    public string AppId { get; set; } = string.Empty;

    public string InstallationId { get; set; } = string.Empty;

    public IGitHubClientForApp AppClient => _appClient ??= clientFactory.CreateForApp(AppId);

    public IConnection GraphQLClient => _graphQLClient ??= clientFactory.CreateForGraphQL(InstallationId);

    public IGitHubClientForInstallation InstallationClient => _installationClient ??= clientFactory.CreateForInstallation(InstallationId);

    public IGitHubClientForUser UserClient => _userClient ??= clientFactory.CreateForUser();

    public GitHubOptions GitHubOptions => githubOptions.CurrentValue;

    public WebhookOptions WebhookOptions => webhookOptions.CurrentValue;
}
