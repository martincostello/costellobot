// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public static class IGitHubClientExtensions
{
    public static IWorkflowRunsClient WorkflowRuns(this IGitHubClient client)
        => new WorkflowRunsClient(new ApiConnection(client.Connection));

    public static async Task RepositoryDispatchAsync(this IGitHubClient client, string owner, string name, object body)
    {
        // See https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28#create-a-repository-dispatch-event
        var uri = new Uri($"repos/{owner}/{name}/dispatches", UriKind.Relative);
        var status = await client.Connection.Post(uri, body, "application/vnd.github+json");

        if (status is not System.Net.HttpStatusCode.NoContent)
        {
            throw new ApiException($"Failed to create repository dispatch event for {owner}/{name}.", status);
        }
    }
}
