// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Infrastructure;

namespace MartinCostello.Costellobot.Registries;

public static class MavenPackageRegistryTests
{
    [Theory]
    [InlineData("io.opentelemetry.instrumentation:opentelemetry-instrumentation-bom", "2.34.0", new[] { "io.opentelemetry.instrumentation", "io.opentelemetry" })]
    [InlineData("io.opentelemetry:opentelemetry-bom", "1.52.0", new[] { "io.opentelemetry" })]
    [InlineData("io.opentelemetry:opentelemetry-bom", "0.0.0", new string[0])]
    public static async Task Can_Get_Package_Owners(string id, string version, string[] expected)
    {
        // Arrange
        var repository = new RepositoryId("some-org", "some-repo");

        var options = await new HttpClientInterceptorOptions()
            .ThrowsOnMissingRegistration()
            .RegisterBundleFromResourceStreamAsync("maven", cancellationToken: TestContext.Current.CancellationToken);

        using var client = options.CreateHttpClient();
        client.BaseAddress = new Uri("https://repo.maven.apache.org/maven2/");

        using var cache = new ApplicationCache();

        var target = new MavenPackageRegistry(client, cache);

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

    [Theory]
    [InlineData("opentelemetry-bom", "1.52.0")]
    [InlineData("", "1.52.0")]
    [InlineData(":", "1.52.0")]
    [InlineData("io.opentelemetry:", "1.52.0")]
    [InlineData(":opentelemetry-bom", "1.52.0")]
    [InlineData("io.opentelemetry:opentelemetry-bom:pom", "1.52.0")]
    [InlineData("io..opentelemetry:opentelemetry-bom", "1.52.0")]
    [InlineData("io.opentelemetry.:opentelemetry-bom", "1.52.0")]
    [InlineData("..:opentelemetry-bom", "1.52.0")]
    [InlineData("io/opentelemetry:opentelemetry-bom", "1.52.0")]
    [InlineData("io.opentelemetry:../opentelemetry-bom", "1.52.0")]
    [InlineData("io.opentelemetry:..", "1.52.0")]
    [InlineData("io.opentelemetry:opentelemetry-bom", "")]
    [InlineData("io.opentelemetry:opentelemetry-bom", "..")]
    [InlineData("io.opentelemetry:opentelemetry-bom", "1.52.0/../1.0.0")]
    public static async Task Does_Not_Query_Registry_For_Invalid_Coordinates(string id, string version)
    {
        // Arrange
        var repository = new RepositoryId("some-org", "some-repo");

        var options = new HttpClientInterceptorOptions().ThrowsOnMissingRegistration();

        using var client = options.CreateHttpClient();
        client.BaseAddress = new Uri("https://repo.maven.apache.org/maven2/");

        using var cache = new ApplicationCache();

        var target = new MavenPackageRegistry(client, cache);

        // Act
        var actual = await target.GetPackageOwnersAsync(
            repository,
            id,
            version,
            TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBeEmpty();
    }

    [Fact]
    public static async Task Throws_If_Registry_Returns_Server_Error()
    {
        // Arrange
        var repository = new RepositoryId("some-org", "some-repo");

        var options = new HttpClientInterceptorOptions().ThrowsOnMissingRegistration();

        new HttpRequestInterceptionBuilder()
            .Requests()
            .ForHttps()
            .ForHost("repo.maven.apache.org")
            .ForPath("maven2/io/opentelemetry/opentelemetry-bom/1.52.0/opentelemetry-bom-1.52.0.pom")
            .ForMethod(HttpMethod.Head)
            .Responds()
            .WithStatus(System.Net.HttpStatusCode.InternalServerError)
            .RegisterWith(options);

        using var client = options.CreateHttpClient();
        client.BaseAddress = new Uri("https://repo.maven.apache.org/maven2/");

        using var cache = new ApplicationCache();

        var target = new MavenPackageRegistry(client, cache);

        // Act and Assert
        await Should.ThrowAsync<HttpRequestException>(
            () => target.GetPackageOwnersAsync(
                repository,
                "io.opentelemetry:opentelemetry-bom",
                "1.52.0",
                TestContext.Current.CancellationToken));
    }
}
