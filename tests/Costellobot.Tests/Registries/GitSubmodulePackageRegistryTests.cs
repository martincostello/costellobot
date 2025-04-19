// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using NSubstitute;
using Octokit;

namespace MartinCostello.Costellobot.Registries;

public static class GitSubmodulePackageRegistryTests
{
    [Theory]
    [InlineData("src/submodules/foo", "deadbee", new string[0])]
    [InlineData("src/submodules/googletest", "7735334", new[] { "https://github.com/google" })]
    public static async Task Can_Get_Package_Owners(string id, string version, string[] expected)
    {
        // Arrange
        var repository = new RepositoryId("dotnet", "aspnetcore");

        var options = await new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .RegisterBundleAsync(Path.Combine("Bundles", "github-submodules.json"), cancellationToken: TestContext.Current.CancellationToken);

        using var adapter = new Octokit.Internal.HttpClientAdapter(options.CreateHttpMessageHandler);
        var connection = new Connection(new("costellobot", "1.0.0"), adapter);
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

        var target = new GitSubmodulePackageRegistry(context);

        // Act
        var actual = await target.GetPackageOwnersAsync(
            repository,
            id,
            version);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBe(expected);
    }
}
