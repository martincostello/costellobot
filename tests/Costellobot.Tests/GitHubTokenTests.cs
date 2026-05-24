// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Infrastructure;
using MartinCostello.Costellobot.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace MartinCostello.Costellobot;

[Collection(HttpServerCollection.Name)]
public sealed class GitHubTokenTests(HttpServerFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<HttpServerFixture>(fixture, outputHelper)
{
    [Fact]
    public async Task Can_Request_Token_With_GitHub_Oidc_Authentication()
    {
        // Arrange
        await ConfigureGitHubOidcAsync();

        var jwt = CertificateFixture.CreateToken(
            repository: "martincostello/costellobot",
            workflow: "benchmark");

        var request = new GitHubTokenRequest() { Profile = "benchmarks" };

        using var client = Fixture.CreateHttpClientForApp();
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt);

        // Act
        using var actual = await client.PostAsJsonAsync("/github-token", request, CancellationToken);

        // Assert
        actual.StatusCode.ShouldBe(HttpStatusCode.OK);
        actual.Headers.CacheControl.ShouldNotBeNull();
        actual.Headers.CacheControl.NoCache.ShouldBeTrue();
        actual.Headers.CacheControl.NoStore.ShouldBeTrue();
        actual.Headers.GetValues("Pragma").ShouldBe(["no-cache"]);
        actual.Content.Headers.Expires.ShouldBe(DateTimeOffset.UnixEpoch);

        var response = await actual.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);

        response.TryGetProperty("token", out var token).ShouldBeTrue();
        token.ValueKind.ShouldBe(JsonValueKind.String);
        token.GetString().ShouldBe("costellobot-benchmarks-write-secret");
    }

    [Fact]
    public async Task Cannot_Request_Token_Without_Authentication()
    {
        // Arrange
        await ConfigureGitHubOidcAsync();

        var request = new GitHubTokenRequest() { Profile = "benchmarks" };

        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var actual = await client.PostAsJsonAsync("/github-token", request, CancellationToken);

        // Assert
        actual.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var response = await actual.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);

        response.TryGetProperty("token", out var subject).ShouldBeFalse();
    }

    [Fact]
    public async Task Cannot_Request_Token_Without_GitHub_Oidc_Authentication()
    {
        // Arrange
        await ConfigureGitHubOidcAsync();

        var request = new GitHubTokenRequest() { Profile = "benchmarks" };

        using var client = Fixture.CreateHttpClientForApp();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");

        // Act
        using var actual = await client.PostAsJsonAsync("/github-token", request, CancellationToken);

        // Assert
        actual.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var response = await actual.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);

        response.TryGetProperty("token", out var subject).ShouldBeFalse();
    }

    [Theory]
    [InlineData("write", "octo-org/octo-repo", "octo-org", "ci", HttpStatusCode.Forbidden)]
    [InlineData("benchmarks", "martincostello/costellobot", "martincostello", "build", HttpStatusCode.Forbidden)]
    [InlineData("benchmarks", "martincostello/sqllocaldb", "martincostello", "benchmark", HttpStatusCode.BadRequest)]
    public async Task Cannot_Request_Token_With_GitHub_Oidc_Authentication_With_Incorrect_Claims(
        string profile,
        string repository,
        string repositoryOwner,
        string workflow,
        HttpStatusCode expected)
    {
        // Arrange
        await ConfigureGitHubOidcAsync();

        var token = CertificateFixture.CreateToken(
            repository: repository,
            repositoryOwner: repositoryOwner,
            workflow: workflow);

        var request = new GitHubTokenRequest() { Profile = profile };

        using var client = Fixture.CreateHttpClientForApp();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act
        using var actual = await client.PostAsJsonAsync("/github-token", request, CancellationToken);

        // Assert
        actual.StatusCode.ShouldBe(expected);

        var response = await actual.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        response.TryGetProperty("token", out var subject).ShouldBeFalse();
    }

    [Fact]
    public async Task Cannot_Request_Token_When_Disabled()
    {
        // Arrange
        Fixture.EnableGitHubTokenExchange(enabled: false);

        await ConfigureGitHubOidcAsync();

        var jwt = CertificateFixture.CreateToken(
            repository: "martincostello/costellobot",
            workflow: "benchmark");

        var request = new GitHubTokenRequest() { Profile = "benchmarks" };

        using var client = Fixture.CreateHttpClientForApp();
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt);

        // Act
        using var actual = await client.PostAsJsonAsync("/github-token", request, CancellationToken);

        // Assert
        actual.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    private async Task ConfigureGitHubOidcAsync()
    {
        var key = JsonWebKeyConverter.ConvertFromRSASecurityKey(CertificateFixture.GetSecurityKey());

        var keySet = new JsonWebKeySet();
        keySet.Keys.Add(key);

        await Fixture.Interceptor.RegisterBundleFromResourceStreamAsync("github-oidc", cancellationToken: CancellationToken);
        Fixture.Interceptor.RegisterGet("https://token.actions.githubusercontent.local/.well-known/jwks", JsonSerializer.Serialize(keySet));
    }
}
