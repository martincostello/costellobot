// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Infrastructure;
using MartinCostello.Costellobot.Models;

namespace MartinCostello.Costellobot;

[Collection<AppCollection>]
public sealed class BadgeTests(AppFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<AppFixture>(fixture, outputHelper)
{
    [Theory]
    [InlineData("security", null)]
    [InlineData("security", ".json")]
    [InlineData("release", null)]
    [InlineData("release", ".json")]
    public async Task Cannot_Get_Badge_Without_Signature(string type, string? format)
    {
        // Arrange
        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/{type}/github-user/github-repo{format}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("", ".json")]
    [InlineData(" ", null)]
    [InlineData(" ", ".json")]
    [InlineData("invalid-signature", null)]
    [InlineData("invalid-signature", ".json")]
    [InlineData("++++++++++++++++++++++++++++", null)]
    [InlineData("++++++++++++++++++++++++++++", ".json")]
    [InlineData("============================", null)]
    [InlineData("============================", ".json")]
    [InlineData("////////////////////////////", null)]
    [InlineData("////////////////////////////", ".json")]
    [InlineData("gAKMaYt06qT+tWTWAEJo54nc15Q=", null)]
    [InlineData("gAKMaYt06qT+tWTWAEJo54nc15Q=", ".json")]
    [InlineData("lm2VKg2sppYxul0S2SxWb0txKu7jBfm6+qqYi5tvP2I=", null)]
    [InlineData("lm2VKg2sppYxul0S2SxWb0txKu7jBfm6+qqYi5tvP2I=", ".json")]
    [InlineData("fSANxeXb07Skc0tV/ZHUv8lGo/iKi+OpGdLPb6BbmfLPQP2bEHZL7fAhT0BTivCi", null)]
    [InlineData("fSANxeXb07Skc0tV/ZHUv8lGo/iKi+OpGdLPb6BbmfLPQP2bEHZL7fAhT0BTivCi", ".json")]
    [InlineData("wlP/xeSHaugTF0Y1Lf8HKCub9nYbtXBZ85Ghn92ttE96YDUvwxAtmSuB64XKrDAW+EjcBTO35c0TJx0jWWDT6Q==", null)]
    [InlineData("wlP/xeSHaugTF0Y1Lf8HKCub9nYbtXBZ85Ghn92ttE96YDUvwxAtmSuB64XKrDAW+EjcBTO35c0TJx0jWWDT6Q==", ".json")]
    public async Task Cannot_Get_Badge_Without_Valid_Signature(string signature, string? format)
    {
        // Arrange
        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/security/github-user/github-repo{format}?s={signature}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Can_Get_Security_Badge_Json_When_No_Access(HttpStatusCode statusCode)
    {
        // Arrange
        string owner = "github-user";
        string repo = "github-repo";
        string signature = CreateSignature("security", owner, repo);

        RegisterGetAccessToken();

        Fixture.Interceptor.RegisterGet($"https://api.github.com/repos/{owner}/{repo}/code-scanning/alerts?state=open", "{}", statusCode);
        Fixture.Interceptor.RegisterGet($"https://api.github.com/repos/{owner}/{repo}/dependabot/alerts?state=open", "{}", statusCode);
        Fixture.Interceptor.RegisterGet($"https://api.github.com/repos/{owner}/{repo}/secret-scanning/alerts?state=open", "{}", statusCode);

        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/security/{owner}/{repo}.json?s={Uri.EscapeDataString(signature)}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl.NoCache.ShouldBeTrue();

        var badge = await response.Content.ReadFromJsonAsync<Badge>(CancellationToken);

        badge.ShouldNotBeNull();
        badge.Color.ShouldBe("brightgreen");
        badge.IsError.ShouldBeFalse();
        badge.Label.ShouldBe("security");
        badge.Message.ShouldBe("0");
        badge.NamedLogo.ShouldBe("github");
        badge.SchemaVersion.ShouldBe(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Can_Get_Security_Badge_Url_When_No_Access(HttpStatusCode statusCode)
    {
        // Arrange
        string owner = "github-user";
        string repo = "github-repo";
        string signature = CreateSignature("security", owner, repo);

        RegisterGetAccessToken();

        Fixture.Interceptor.RegisterGet($"https://api.github.com/repos/{owner}/{repo}/code-scanning/alerts?state=open", "{}", statusCode);
        Fixture.Interceptor.RegisterGet($"https://api.github.com/repos/{owner}/{repo}/dependabot/alerts?state=open", "{}", statusCode);
        Fixture.Interceptor.RegisterGet($"https://api.github.com/repos/{owner}/{repo}/secret-scanning/alerts?state=open", "{}", statusCode);

        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/security/{owner}/{repo}?s={Uri.EscapeDataString(signature)}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldBe(new("https://img.shields.io/badge/security-0-brightgreen?logo=github"));
        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl.NoCache.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_Get_Security_Badge_Json_When_No_Alerts()
    {
        // Arrange
        string owner = "github-user";
        string repo = "github-repo";
        string signature = CreateSignature("security", owner, repo);

        RegisterGetAccessToken();

        Fixture.Interceptor.RegisterGetJson($"https://api.github.com/repos/{owner}/{repo}/code-scanning/alerts?state=open", Array.Empty<object>());
        Fixture.Interceptor.RegisterGetJson($"https://api.github.com/repos/{owner}/{repo}/dependabot/alerts?state=open", Array.Empty<object>());
        Fixture.Interceptor.RegisterGetJson($"https://api.github.com/repos/{owner}/{repo}/secret-scanning/alerts?state=open", Array.Empty<object>());

        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/security/{owner}/{repo}.json?s={Uri.EscapeDataString(signature)}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl.NoCache.ShouldBeTrue();

        var badge = await response.Content.ReadFromJsonAsync<Badge>(CancellationToken);

        badge.ShouldNotBeNull();
        badge.Color.ShouldBe("brightgreen");
        badge.IsError.ShouldBeFalse();
        badge.Label.ShouldBe("security");
        badge.Message.ShouldBe("0");
        badge.NamedLogo.ShouldBe("github");
        badge.SchemaVersion.ShouldBe(1);
    }

    [Fact]
    public async Task Can_Get_Security_Badge_Url_When_No_Alerts()
    {
        // Arrange
        string owner = "github-user";
        string repo = "github-repo";
        string signature = CreateSignature("security", owner, repo);

        RegisterGetAccessToken();

        Fixture.Interceptor.RegisterGetJson($"https://api.github.com/repos/{owner}/{repo}/code-scanning/alerts?state=open", Array.Empty<object>());
        Fixture.Interceptor.RegisterGetJson($"https://api.github.com/repos/{owner}/{repo}/dependabot/alerts?state=open", Array.Empty<object>());
        Fixture.Interceptor.RegisterGetJson($"https://api.github.com/repos/{owner}/{repo}/secret-scanning/alerts?state=open", Array.Empty<object>());

        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/security/{owner}/{repo}?s={Uri.EscapeDataString(signature)}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldBe(new("https://img.shields.io/badge/security-0-brightgreen?logo=github"));
        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl.NoCache.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_Get_Security_Badge_Json_When_Some_Alerts()
    {
        // Arrange
        string owner = "github-user";
        string repo = "github-repo";
        string signature = CreateSignature("security", owner, repo);

        RegisterGetAccessToken();

        Fixture.Interceptor.RegisterGetJson($"https://api.github.com/repos/{owner}/{repo}/code-scanning/alerts?state=open", Alerts(1));
        Fixture.Interceptor.RegisterGetJson($"https://api.github.com/repos/{owner}/{repo}/dependabot/alerts?state=open", Alerts(3));
        Fixture.Interceptor.RegisterGetJson($"https://api.github.com/repos/{owner}/{repo}/secret-scanning/alerts?state=open", Alerts(5));

        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/security/{owner}/{repo}.json?s={Uri.EscapeDataString(signature)}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl.NoCache.ShouldBeTrue();

        var badge = await response.Content.ReadFromJsonAsync<Badge>(CancellationToken);

        badge.ShouldNotBeNull();
        badge.Color.ShouldBe("red");
        badge.IsError.ShouldBeTrue();
        badge.Label.ShouldBe("security");
        badge.Message.ShouldBe("9");
        badge.NamedLogo.ShouldBe("github");
        badge.SchemaVersion.ShouldBe(1);

        static object[] Alerts(int count)
            => [.. Enumerable.Range(0, count).Select((i) => new { })];
    }

    [Fact]
    public async Task Can_Get_Security_Badge_Url_When_Some_Alerts()
    {
        // Arrange
        string owner = "github-user";
        string repo = "github-repo";
        string signature = CreateSignature("security", owner, repo);

        RegisterGetAccessToken();

        Fixture.Interceptor.RegisterGetJson($"https://api.github.com/repos/{owner}/{repo}/code-scanning/alerts?state=open", Alerts(1));
        Fixture.Interceptor.RegisterGetJson($"https://api.github.com/repos/{owner}/{repo}/dependabot/alerts?state=open", Alerts(3));
        Fixture.Interceptor.RegisterGetJson($"https://api.github.com/repos/{owner}/{repo}/secret-scanning/alerts?state=open", Alerts(5));

        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/security/{owner}/{repo}?s={Uri.EscapeDataString(signature)}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldBe(new("https://img.shields.io/badge/security-9-red?logo=github"));
        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl.NoCache.ShouldBeTrue();

        static object[] Alerts(int count)
            => [.. Enumerable.Range(0, count).Select((i) => new { })];
    }

    [Theory]
    [InlineData(null)]
    [InlineData(".json")]
    public async Task Cannot_Get_Release_Badge_When_No_Releases(string? format)
    {
        // Arrange
        string owner = "github-user";
        string repo = "github-repo";
        string signature = CreateSignature("release", owner, repo);

        RegisterGetAccessToken();

        Fixture.Interceptor.RegisterGet($"https://api.github.com/repos/{owner}/{repo}/releases/latest", "{}", HttpStatusCode.NotFound);
        Fixture.Interceptor.RegisterGet($"https://api.github.com/repos/{owner}/{repo}/releases?per_page=1", "[]");

        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/release/{owner}/{repo}{format}?s={Uri.EscapeDataString(signature)}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("v1.2.3-preview.1", "1.2.3-preview.1")]
    [InlineData("My Product 1.2.3", "1.2.3")]
    public async Task Can_Get_Release_Badge_Json_When_Latest_Release_Found(string name, string expected)
    {
        // Arrange
        string owner = "github-user";
        string repo = "github-repo";
        string signature = CreateSignature("release", owner, repo);

        RegisterGetAccessToken();

        /*lang=json,strict*/
        string json = $$"""{ "name": "{{name}}" }""";

        Fixture.Interceptor.RegisterGet($"https://api.github.com/repos/{owner}/{repo}/releases/latest", json);

        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/release/{owner}/{repo}.json?s={Uri.EscapeDataString(signature)}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl.NoCache.ShouldBeTrue();

        var badge = await response.Content.ReadFromJsonAsync<Badge>(CancellationToken);

        badge.ShouldNotBeNull();
        badge.Color.ShouldBe("blue");
        badge.IsError.ShouldBeFalse();
        badge.Label.ShouldBe("release");
        badge.Message.ShouldBe(expected);
        badge.NamedLogo.ShouldBe("github");
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("v1.2.3-preview.1", "1.2.3--preview.1")]
    [InlineData("My Product 1.2.3", "1.2.3")]
    public async Task Can_Get_Release_Badge_Url_When_Latest_Release_Found(string name, string expected)
    {
        // Arrange
        string owner = "github-user";
        string repo = "github-repo";
        string signature = CreateSignature("release", owner, repo);

        RegisterGetAccessToken();

        /*lang=json,strict*/
        string json = $$"""{ "name": "{{name}}" }""";

        Fixture.Interceptor.RegisterGet($"https://api.github.com/repos/{owner}/{repo}/releases/latest", json);

        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/release/{owner}/{repo}?s={Uri.EscapeDataString(signature)}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldBe(new($"https://img.shields.io/badge/release-{expected}-blue?logo=github"));
        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl.NoCache.ShouldBeTrue();
    }

    private static string CreateSignature(string type, string owner, string repo)
    {
        var key = Encoding.UTF8.GetBytes("badges-key");
        var data = Encoding.UTF8.GetBytes($"badge-{type}-{owner}-{repo}");

        var hmac = HMACSHA256.HashData(key, data);
        return Convert.ToBase64String(hmac);
    }
}
