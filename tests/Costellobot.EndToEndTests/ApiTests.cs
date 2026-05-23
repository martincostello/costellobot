// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MartinCostello.Costellobot;

public class ApiTests(AppFixture fixture, ITestOutputHelper outputHelper) : EndToEndTest(fixture, outputHelper)
{
    [Fact]
    public async Task Can_Get_Secret_With_GitHub_Oidc_Token()
    {
        // Arrange
        var requestUrl = Environment.GetEnvironmentVariable("ACTIONS_ID_TOKEN_REQUEST_URL");
        var requestToken = Environment.GetEnvironmentVariable("ACTIONS_ID_TOKEN_REQUEST_TOKEN");

        Assert.SkipWhen(string.IsNullOrEmpty(requestUrl), "GitHub OIDC request URL is not available.");
        Assert.SkipWhen(string.IsNullOrEmpty(requestToken), "GitHub OIDC request token is not available.");

        using var client = Fixture.CreateClient();

        string jwt;

        using (var message = new HttpRequestMessage(HttpMethod.Get, requestUrl))
        {
            message.Headers.Authorization = new("Bearer", requestToken);

            using var oidcResponse = await client.SendAsync(message, CancellationToken);
            oidcResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

            using var tokenResponse = await oidcResponse.Content.ReadFromJsonAsync<JsonDocument>(CancellationToken);
            tokenResponse.ShouldNotBeNull();

            tokenResponse.RootElement.TryGetProperty("value", out var value).ShouldBeTrue();
            value.ValueKind.ShouldBe(JsonValueKind.String);
            jwt = value.GetString()!;
            jwt.ShouldNotBeNullOrWhiteSpace();
        }

        // Act
        using var response = await client.PostAsJsonAsync("/github-oidc", new { profile = "self-test" }, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var actual = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);

        actual.TryGetProperty("token", out var token).ShouldBeTrue();
        token.ValueKind.ShouldBe(JsonValueKind.String);
        token.GetString().ShouldBe("not-a-real-secret");
    }
}
