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
public sealed class GitHubTokenTests(HttpServerFixture fixture, ITestOutputHelper outputHelper)
    : IntegrationTests<HttpServerFixture>(fixture, outputHelper)
{
    [Fact]
    public async Task Can_Request_Token_With_GitHub_Oidc_Authentication_For_App_Installation()
    {
        // Arrange
        await ConfigureGitHubOidcAsync();

        var jwt = CertificateFixture.CreateToken(
            repository: "martincostello/costellobot",
            workflow: "build.yml");

        var request = new GitHubTokenRequest() { Profile = "self-test-app" };

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
        token.GetString().ShouldBe("not-a-real-github-app-installation-token");

        response.TryGetProperty("type", out var type).ShouldBeTrue();
        type.ValueKind.ShouldBe(JsonValueKind.String);
        type.GetString().ShouldBe("app");

        response.TryGetProperty("appId", out var appId).ShouldBeTrue();
        appId.ValueKind.ShouldBe(JsonValueKind.Number);
        appId.GetInt64().ShouldBe(183256);

        response.TryGetProperty("appSlug", out var appSlug).ShouldBeTrue();
        appSlug.ValueKind.ShouldBe(JsonValueKind.String);
        appSlug.GetString().ShouldBe("costellobot");
    }

    [Fact]
    public async Task Can_Request_Token_With_GitHub_Oidc_Authentication_For_User()
    {
        // Arrange
        await ConfigureGitHubOidcAsync();

        var jwt = CertificateFixture.CreateToken(
            repository: "martincostello/costellobot",
            workflow: "build.yml");

        var request = new GitHubTokenRequest() { Profile = "self-test-user" };

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
        token.GetString().ShouldBe("costellobot-self-test-secret");

        response.TryGetProperty("type", out var type).ShouldBeTrue();
        type.ValueKind.ShouldBe(JsonValueKind.String);
        type.GetString().ShouldBe("user");

        response.TryGetProperty("appId", out _).ShouldBeFalse();
        response.TryGetProperty("appSlug", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Cannot_Request_Token_Without_Authentication()
    {
        // Arrange
        await ConfigureGitHubOidcAsync();

        var request = new GitHubTokenRequest() { Profile = "self-test-user" };

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

        var request = new GitHubTokenRequest() { Profile = "self-test-user" };

        using var client = Fixture.CreateHttpClientForApp();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");

        // Act
        using var actual = await client.PostAsJsonAsync("/github-token", request, CancellationToken);

        // Assert
        actual.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var response = await actual.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);

        response.TryGetProperty("token", out var subject).ShouldBeFalse();
    }

    [Fact]
    public async Task Cannot_Request_Token_With_GitHub_Oidc_Authentication_When_Jwt_Is_Not_Yet_Valid()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var jwt = CertificateFixture.CreateToken(
            repository: "martincostello/costellobot",
            workflow: "build.yml",
            notBefore: now.AddMinutes(2),
            expiresAt: now.AddMinutes(4),
            issuedAt: now);

        // Act and Assert
        await AssertGitHubOidcAuthenticationFailsAsync(jwt);
    }

    [Fact]
    public async Task Cannot_Request_Token_With_GitHub_Oidc_Authentication_When_Jwt_Has_Expired()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var jwt = CertificateFixture.CreateToken(
            repository: "martincostello/costellobot",
            workflow: "build.yml",
            notBefore: now.AddMinutes(-4),
            expiresAt: now.AddMinutes(-2),
            issuedAt: now.AddMinutes(-4));

        // Act and Assert
        await AssertGitHubOidcAuthenticationFailsAsync(jwt);
    }

    [Fact]
    public async Task Cannot_Request_Token_With_GitHub_Oidc_Authentication_When_Jwt_Signature_Is_Invalid()
    {
        // Arrange
        var jwt = CertificateFixture.CreateToken(
            repository: "martincostello/costellobot",
            workflow: "build.yml");

        var segments = jwt.Split('.');
        var signature = Base64UrlEncoder.DecodeBytes(segments[2]);
        signature[0] ^= 0xFF;
        segments[2] = Base64UrlEncoder.Encode(signature);

        // Act and Assert
        await AssertGitHubOidcAuthenticationFailsAsync(string.Join(".", segments));
    }

    [Fact]
    public async Task Cannot_Request_Token_With_GitHub_Oidc_Authentication_When_Jwt_Uses_Invalid_Signature_Algorithm()
    {
        // Arrange
        var jwt = CertificateFixture.CreateUnsignedToken(
            repository: "martincostello/costellobot",
            workflow: "build.yml");

        // Act and Assert
        await AssertGitHubOidcAuthenticationFailsAsync(jwt);
    }

    [Fact]
    public async Task Cannot_Request_Token_With_GitHub_Oidc_Authentication_When_Issuer_Is_Incorrect()
    {
        // Arrange
        var jwt = CertificateFixture.CreateToken(
            repository: "martincostello/costellobot",
            workflow: "build.yml",
            issuer: "https://token.actions.githubusercontent.invalid");

        // Act and Assert
        await AssertGitHubOidcAuthenticationFailsAsync(jwt);
    }

    [Fact]
    public async Task Cannot_Request_Token_With_GitHub_Oidc_Authentication_When_Audience_Is_Incorrect()
    {
        // Arrange
        var jwt = CertificateFixture.CreateToken(
            repository: "martincostello/costellobot",
            workflow: "build.yml",
            audience: "https://github.com/octocat");

        // Act and Assert
        await AssertGitHubOidcAuthenticationFailsAsync(jwt);
    }

    [Theory]
    [InlineData("write", "octo-org/octo-repo", "octo-org", "ci.yml", HttpStatusCode.Forbidden)]
    [InlineData("", "martincostello/costellobot", "martincostello", "build.yml", HttpStatusCode.BadRequest)]
    [InlineData("benchmarks", "martincostello/costellobot", "martincostello", "build.yml", HttpStatusCode.Forbidden)]
    [InlineData("benchmarks", "martincostello/unknown", "martincostello", "build.yml", HttpStatusCode.Forbidden)]
    [InlineData("unknown", "martincostello/costellobot", "martincostello", "benchmark.yml", HttpStatusCode.BadRequest)]
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
            workflow: "build.yml");

        var request = new GitHubTokenRequest() { Profile = "self-test-user" };

        using var client = Fixture.CreateHttpClientForApp();
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt);

        // Act
        using var actual = await client.PostAsJsonAsync("/github-token", request, CancellationToken);

        // Assert
        actual.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Token_Requests_Are_Rate_Limited_Per_Token()
    {
        // Arrange
        await ConfigureGitHubOidcAsync();

        var jwt = CertificateFixture.CreateToken(
            repository: "martincostello/costellobot",
            workflow: "build.yml");

        var request = new GitHubTokenRequest() { Profile = "self-test-user" };

        using var client = Fixture.CreateHttpClientForApp();
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt);

        for (int i = 0; i < 5; i++)
        {
            using var permitted = await client.PostAsJsonAsync("/github-token", request, CancellationToken);
            permitted.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Act
        using var actual = await client.PostAsJsonAsync("/github-token", request, CancellationToken);

        // Assert
        actual.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    private async Task AssertGitHubOidcAuthenticationFailsAsync(string jwt)
    {
        await ConfigureGitHubOidcAsync();

        var request = new GitHubTokenRequest() { Profile = "self-test-user" };

        using var client = Fixture.CreateHttpClientForApp();
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt);

        using var actual = await client.PostAsJsonAsync("/github-token", request, CancellationToken);

        actual.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var response = await actual.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        response.TryGetProperty("token", out _).ShouldBeFalse();
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
