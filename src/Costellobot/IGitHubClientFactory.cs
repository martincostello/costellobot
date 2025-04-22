// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;
using IConnection = Octokit.GraphQL.IConnection;

namespace MartinCostello.Costellobot;

public interface IGitHubClientFactory
{
    IGitHubClientForApp CreateForApp(string appId);

    IGitHubClientForInstallation CreateForInstallation(string installationId);

    IConnection CreateForGraphQL(string installationId);

    IGitHubClientForUser CreateForUser();
}
