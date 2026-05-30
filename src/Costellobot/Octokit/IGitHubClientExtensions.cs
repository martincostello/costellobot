// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;

namespace Octokit;

public static class IGitHubClientExtensions
{
    private const string AcceptsDiff = "application/vnd.github.v3.diff";
    private const string AcceptsJson = "application/vnd.github+json";

    public static IWorkflowRunsClient WorkflowRuns(this IGitHubClient client)
        => new WorkflowRunsClient(new ApiConnection(client.Connection));

    public static async Task<string> GetDiffAsync(
        this IGitHubClient client,
        string pullRequestUrl,
        CancellationToken cancellationToken)
    {
        // See https://docs.github.com/rest/pulls/pulls?apiVersion=2022-11-28#get-a-pull-request
        var parameters = new Dictionary<string, string>(0);

        var response = await client.Connection.Get<string>(
            new(pullRequestUrl, UriKind.Absolute),
            parameters,
            AcceptsDiff,
            cancellationToken);

        return response.Body;
    }

    public static async Task RepositoryDispatchAsync(
        this IGitHubClient client,
        string owner,
        string name,
        object body,
        CancellationToken cancellationToken)
    {
        // See https://docs.github.com/rest/repos/repos?apiVersion=2022-11-28#create-a-repository-dispatch-event
        var uri = new Uri($"repos/{owner}/{name}/dispatches", UriKind.Relative);
        var status = await client.Connection.Post(uri, body, AcceptsJson, cancellationToken);

        if (status is not HttpStatusCode.NoContent)
        {
            throw new ApiException($"Failed to create repository dispatch event for {owner}/{name}.", status);
        }
    }

    public static async Task<AccessToken> CreateInstallationTokenAsync(
        this IGitHubClient client,
        long installationId,
        IList<string> targetRepositories,
        IDictionary<string, string> requiredPermissions,
        CancellationToken cancellationToken)
    {
        // See https://docs.github.com/rest/apps/apps?apiVersion=2026-03-10#create-an-installation-access-token-for-an-app
        var body = new
        {
            repositories = targetRepositories,
            permissions = requiredPermissions,
        };

        var uri = new Uri($"app/installations/{installationId}/access_tokens", UriKind.Relative);

        var response = await client.Connection.Post<AccessToken>(
            uri,
            body,
            AcceptsJson,
            "application/json",
            cancellationToken: cancellationToken);

        if (response.HttpResponse.StatusCode is not HttpStatusCode.Created)
        {
            throw new ApiException($"Failed to create token for installation {installationId}.", response.HttpResponse.StatusCode);
        }

        return response.Body;
    }
}
