// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;
using IConnection = Octokit.GraphQL.IConnection;

namespace MartinCostello.Costellobot;

public interface IGitHubClientFactory
{
    // TODO Remove once the app ID flows everywhere
    IGitHubClientForApp CreateForApp() => CreateForApp("183256");

    IGitHubClientForApp CreateForApp(string appId);

    // TODO Remove once the installation ID flows everywhere
    IGitHubClientForInstallation CreateForInstallation() => CreateForInstallation("24364748");

    IGitHubClientForInstallation CreateForInstallation(string installationId);

    IConnection CreateForGraphQL(string installationId);

    IGitHubClientForUser CreateForUser();
}
