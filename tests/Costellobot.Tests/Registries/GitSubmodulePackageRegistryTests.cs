// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
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
        string owner = "dotnet";
        string repository = "aspnetcore";

        var options = await new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .RegisterBundleAsync(Path.Combine("Bundles", "github-submodules.json"));

        using var adapter = new Octokit.Internal.HttpClientAdapter(options.CreateHttpMessageHandler);
        var connection = new Connection(new("costellobot", "1.0.0"), adapter);
        var client = new GitHubClientAdapter(connection);
        var graphConnection = Substitute.For<Octokit.GraphQL.IConnection>();

        var target = new GitSubmodulePackageRegistry(client, graphConnection);

        // Act
        var actual = await target.GetPackageOwnersAsync(
            owner,
            repository,
            id,
            version);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBe(expected);
    }
}
