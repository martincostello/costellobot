// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;
using IConnection = Octokit.GraphQL.IConnection;

namespace MartinCostello.Costellobot;

public sealed class GitHubClientFactory(IServiceProvider serviceProvider) : IGitHubClientFactory
{
    public IGitHubClientForApp CreateForApp(string appId)
        => serviceProvider.GetRequiredKeyedService<IGitHubClientForApp>(appId);

    public IGitHubClientForInstallation CreateForInstallation(string installationId)
        => serviceProvider.GetRequiredKeyedService<IGitHubClientForInstallation>(installationId);

    public IConnection CreateForGraphQL(string installationId)
        => serviceProvider.GetRequiredKeyedService<IConnection>(installationId);

    public IGitHubClientForUser CreateForUser()
        => serviceProvider.GetRequiredService<IGitHubClientForUser>();
}
