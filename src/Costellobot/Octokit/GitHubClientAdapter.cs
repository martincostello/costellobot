// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public sealed class GitHubClientAdapter : GitHubClient, IGitHubClientForApp, IGitHubClientForInstallation
{
    public GitHubClientAdapter(ProductHeaderValue productInformation, ICredentialStore credentialStore, Uri baseAddress)
        : base(productInformation, credentialStore, baseAddress)
    {
    }

    public GitHubClientAdapter(IConnection connection)
        : base(connection)
    {
    }
}
