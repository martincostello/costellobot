// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Infrastructure;

namespace MartinCostello.Costellobot.Registries;

public class RubyGemsPackageRegistryTests
{
    [Theory]
    [InlineData("rack", "3.1.16", new[] { "tenderlove", "raggi", "chneukirchen", "ioquatix", "rafaelfranca", "eileencodes" })]
    [InlineData("foo", "1.0.0", new string[0])]
    public async Task Can_Get_Package_Owners(string id, string version, string[] expected)
    {
        // Arrange
        var repository = new RepositoryId("some-org", "some-repo");

        var options = await new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .RegisterBundleFromResourceStreamAsync("ruby-gems", cancellationToken: TestContext.Current.CancellationToken);

        using var client = options.CreateHttpClient();
        client.BaseAddress = new Uri("https://rubygems.org");

        using var cache = new ApplicationCache();

        var target = new RubyGemsPackageRegistry(client, cache);

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
