// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;

namespace MartinCostello.Costellobot.Registries;

public static class NpmPackageRegistryTests
{
    [Theory]
    [InlineData("foo", "0.0", new string[0])]
    [InlineData("foo", "0.0.0", new string[0])]
    [InlineData("@types/node", "18.6.2", new[] { "types" })]
    [InlineData("typescript", "4.6.3", new[] { "typescript-bot" })]
    public static async Task Can_Get_Package_Owners(string id, string version, string[] expected)
    {
        // Arrange
        string owner = "some-org";
        string repository = "some-repo";

        var options = new HttpClientInterceptorOptions()
            .RegisterBundle(Path.Combine("Bundles", "npm-registry.json"))
            .ThrowsOnMissingRegistration();

        using var client = options.CreateHttpClient();

        var target = new NpmPackageRegistry(client);

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
