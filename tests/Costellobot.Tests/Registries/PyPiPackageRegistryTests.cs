// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Infrastructure;

namespace MartinCostello.Costellobot.Registries;

public class PyPiPackageRegistryTests
{
    [Theory]
    [InlineData("foo", "1.2.3", null)]
    [InlineData("aiohttp", "3.12.15", true)]
    [InlineData("boto3", "1.40.11", null)]
    [InlineData("foobar", "4.5.6", false)]
    public static async Task Can_Get_Package_Attestation(string id, string version, bool? expected)
    {
        // Arrange
        var repository = new RepositoryId("some-org", "some-repo");

        var options = await new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .RegisterBundleFromResourceStreamAsync("pypi", cancellationToken: TestContext.Current.CancellationToken);

        using var client = options.CreateHttpClient();
        client.BaseAddress = new Uri("https://pypi.org");

        using var cache = new ApplicationCache();

        var target = new PyPiPackageRegistry(client, cache);

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
    [InlineData("aiohttp", "3.12.15", new[] { "aiohttp team <team@aiohttp.org>" })]
    [InlineData("foo", "1.2.3", new string[0])]
    public async Task Can_Get_Package_Owners(string id, string version, string[] expected)
    {
        // Arrange
        var repository = new RepositoryId("some-org", "some-repo");

        var options = await new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .RegisterBundleFromResourceStreamAsync("pypi", cancellationToken: TestContext.Current.CancellationToken);

        using var client = options.CreateHttpClient();
        client.BaseAddress = new Uri("https://pypi.org");

        using var cache = new ApplicationCache();

        var target = new PyPiPackageRegistry(client, cache);

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
