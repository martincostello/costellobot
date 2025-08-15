// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Infrastructure;
using NSubstitute;
using Octokit;
using Octokit.Internal;

namespace MartinCostello.Costellobot.Registries;

public static class GitHubReleasePackageRegistryTests
{
    [Theory]
    [InlineData("zizmorcore/zizmor", "v1.12.1", new[] { "zizmorcore" })]
    [InlineData("foo", "1.0.0", new string[0])]
    [InlineData("foo/bar", "v1", new string[0])]
    [InlineData("foo/bar/baz", "v1.0.0", new string[0])]
    public static async Task Can_Get_Package_Owners(string id, string version, string[] expected)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = new RepositoryId("some-org", "some-repo");

        var options = await new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .RegisterBundleFromResourceStreamAsync("github-releases", cancellationToken: cancellationToken);

        using var httpClient = new HttpClientAdapter(options.CreateHttpMessageHandler);
        var connection = new Connection(new("costellobot", "1.0.0"), httpClient);
        var client = new GitHubClientAdapter(connection);
        var graphConnection = Substitute.For<Octokit.GraphQL.IConnection>();

        var clientFactory = Substitute.For<IGitHubClientFactory>();

        clientFactory.CreateForGraphQL(GitHubFixtures.InstallationId)
                     .Returns(graphConnection);

        clientFactory.CreateForApp(GitHubFixtures.AppId)
                     .Returns(client);

        clientFactory.CreateForInstallation(GitHubFixtures.InstallationId)
                     .Returns(client);

        clientFactory.CreateForUser()
                     .Returns(client);

        var context = new GitHubWebhookContext(
            clientFactory,
            new GitHubOptions().ToMonitor(),
            new WebhookOptions().ToMonitor())
        {
            AppId = GitHubFixtures.AppId,
            InstallationId = GitHubFixtures.InstallationId,
        };

        using var cache = new ApplicationCache();

        var target = new GitHubReleasePackageRegistry(context, cache);

        // Act
        var actual = await target.GetPackageOwnersAsync(
            repository,
            id,
            version,
            cancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBe(expected);
    }
}
