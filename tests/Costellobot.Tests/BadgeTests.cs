// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Infrastructure;

namespace MartinCostello.Costellobot;

[Collection(AppCollection.Name)]
public sealed class BadgeTests(AppFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<AppFixture>(fixture, outputHelper)
{
    [Theory]
    [InlineData("security")]
    [InlineData("release")]
    public async Task Cannot_Get_Badge_Without_Signature(string type)
    {
        // Arrange
        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/{type}/github-user/github-repo");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-signature")]
    [InlineData("++++++++++++++++++++++++++++")]
    [InlineData("============================")]
    [InlineData("////////////////////////////")]
    [InlineData("gAKMaYt06qT+tWTWAEJo54nc15Q=")]
    [InlineData("lm2VKg2sppYxul0S2SxWb0txKu7jBfm6+qqYi5tvP2I=")]
    [InlineData("fSANxeXb07Skc0tV/ZHUv8lGo/iKi+OpGdLPb6BbmfLPQP2bEHZL7fAhT0BTivCi")]
    [InlineData("wlP/xeSHaugTF0Y1Lf8HKCub9nYbtXBZ85Ghn92ttE96YDUvwxAtmSuB64XKrDAW+EjcBTO35c0TJx0jWWDT6Q==")]
    public async Task Cannot_Get_Badge_Without_Valid_Signature(string signature)
    {
        // Arrange
        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync($"/badge/security/github-user/github-repo?s={signature}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Can_Get_Security_Badge_When_No_Access(HttpStatusCode statusCode)
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
        using var response = await client.GetAsync($"/badge/security/{owner}/{repo}?s={Uri.EscapeDataString(signature)}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldBe(new("https://img.shields.io/badge/security-0-brightgreen?logo=github"));
    }

    [Fact]
    public async Task Can_Get_Security_Badge_When_No_Alerts()
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
        using var response = await client.GetAsync($"/badge/security/{owner}/{repo}?s={Uri.EscapeDataString(signature)}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldBe(new("https://img.shields.io/badge/security-0-brightgreen?logo=github"));
    }

    [Fact]
    public async Task Can_Get_Security_Badge_When_Some_Alerts()
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
        using var response = await client.GetAsync($"/badge/security/{owner}/{repo}?s={Uri.EscapeDataString(signature)}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldBe(new("https://img.shields.io/badge/security-9-red?logo=github"));

        static object[] Alerts(int count)
        {
            return Enumerable.Range(0, count)
                .Select((i) => new { })
                .ToArray();
        }
    }

    [Fact]
    public async Task Cannot_Get_Release_Badge_When_No_Releases()
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
        using var response = await client.GetAsync($"/badge/release/{owner}/{repo}?s={Uri.EscapeDataString(signature)}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3", "v1.2.3")]
    [InlineData("v1.2.3-preview.1", "v1.2.3--preview.1")]
    [InlineData("My Product 1.2.3", "1.2.3")]
    public async Task Can_Get_Release_Badge_When_Latest_Release_Found(string name, string expected)
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
        using var response = await client.GetAsync($"/badge/release/{owner}/{repo}?s={Uri.EscapeDataString(signature)}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldBe(new($"https://img.shields.io/badge/release-{expected}-blue?logo=github"));
    }

    private static string CreateSignature(string type, string owner, string repo)
    {
        var key = Encoding.UTF8.GetBytes("badges-key");
        var data = Encoding.UTF8.GetBytes($"badge-{type}-{owner}-{repo}");

        var hmac = HMACSHA256.HashData(key, data);
        return Convert.ToBase64String(hmac);
    }
}
