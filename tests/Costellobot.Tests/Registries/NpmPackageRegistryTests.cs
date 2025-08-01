// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Infrastructure;

namespace MartinCostello.Costellobot.Registries;

public static class NpmPackageRegistryTests
{
    [Theory]
    [InlineData("foo", "0.0", null)]
    [InlineData("foo", "0.0.0", null)]
    [InlineData("@types/node", "18.6.2", false)]
    [InlineData("typescript", "4.6.3", false)]
    [InlineData("eslint-config-prettier", "10.1.3", false)]
    [InlineData("eslint-config-prettier", "10.1.8", true)]
    [InlineData("eslint-config-prettier", "10.1.9", false)]
    public static async Task Can_Get_Package_Attestation(string id, string version, bool? expected)
    {
        // Arrange
        var repository = new RepositoryId("some-org", "some-repo");

        var options = await new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .RegisterBundleFromResourceStreamAsync("npm-registry", cancellationToken: TestContext.Current.CancellationToken);

        using var client = options.CreateHttpClient();
        client.BaseAddress = new Uri("https://registry.npmjs.org");

        using var cache = new ApplicationCache();

        var target = new NpmPackageRegistry(client, cache);

        // Act
        var actual = await target.GetPackageAttestationAsync(
            repository,
            id,
            version,
            TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData("foo", "0.0", new string[0])]
    [InlineData("foo", "0.0.0", new string[0])]
    [InlineData("@types/node", "18.6.2", new[] { "types" })]
    [InlineData("typescript", "4.6.3", new[] { "typescript-bot" })]
    public static async Task Can_Get_Package_Owners(string id, string version, string[] expected)
    {
        // Arrange
        var repository = new RepositoryId("some-org", "some-repo");

        var options = await new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .RegisterBundleFromResourceStreamAsync("npm-registry", cancellationToken: TestContext.Current.CancellationToken);

        using var client = options.CreateHttpClient();
        client.BaseAddress = new Uri("https://registry.npmjs.org");

        using var cache = new ApplicationCache();

        var target = new NpmPackageRegistry(client, cache);

        // Act
        var actual = await target.GetPackageOwnersAsync(
            repository,
            id,
            version,
            TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBe(expected);
    }
}
