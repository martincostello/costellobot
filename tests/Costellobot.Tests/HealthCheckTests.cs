// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using MartinCostello.Costellobot.Infrastructure;

namespace MartinCostello.Costellobot;

[Collection<HttpServerCollection>]
public sealed class HealthCheckTests(HttpServerFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<HttpServerFixture>(fixture, outputHelper)
{
    public static TheoryData<string> HealthCheckUrls() =>
    [
        "/health/liveness",
        "/health/readiness",
        "/health/startup"
    ];

    [Theory]
    [MemberData(nameof(HealthCheckUrls))]
    public async Task Cannot_Get_Health_Resource_Unauthenticated(string requestUri)
    {
        // Arrange
        using var client = Fixture.CreateHttpClientForApp();

        // Act
        using var response = await client.GetAsync(requestUri, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Found, $"Failed to get {requestUri}. {await response.Content!.ReadAsStringAsync(CancellationToken)}");
        response.Content.Headers.ContentLength.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(HealthCheckUrls))]
    public async Task Cannot_Get_Health_Resource_Unauthenticated_With_Invalid_Token(string requestUri)
    {
        // Arrange
        using var client = Fixture.CreateHttpClientForApp();

        client.DefaultRequestHeaders.Add("x-ms-auth-internal-token", "47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=");

        // Act
        using var response = await client.GetAsync(requestUri, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Found, $"Failed to get {requestUri}. {await response.Content!.ReadAsStringAsync(CancellationToken)}");
        response.Content.Headers.ContentLength.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(HealthCheckUrls))]
    public async Task Can_Get_Health_Resource_Unauthenticated_With_Token(string requestUri)
    {
        // Arrange
        using var client = Fixture.CreateHttpClientForApp();
        client.DefaultRequestHeaders.Add("x-ms-auth-internal-token", "I3eGXFWsxHOxEhULepTolmX9jSd/rcSs9CAjao2DgGk=");

        // Act
        using var response = await client.GetAsync(requestUri, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Failed to get {requestUri}. {await response.Content!.ReadAsStringAsync(CancellationToken)}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe(MediaTypeNames.Application.Json);
        response.Content.Headers.ContentLength.ShouldNotBeNull();
        response.Content.Headers.ContentLength.ShouldNotBe(0);

        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(CancellationToken);

        document.ShouldNotBeNull();
        document.RootElement.TryGetProperty("status", out var status).ShouldBeTrue();
        status.ValueKind.ShouldBe(JsonValueKind.String);
        status.GetString().ShouldBe("Healthy");
    }

    [Theory]
    [MemberData(nameof(HealthCheckUrls))]
    public async Task Can_Get_Health_Resource_Authenticated_As_User(string requestUri)
    {
        // Arrange
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync(requestUri, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Failed to get {requestUri}. {await response.Content!.ReadAsStringAsync(CancellationToken)}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe(MediaTypeNames.Application.Json);
        response.Content.Headers.ContentLength.ShouldNotBeNull();
        response.Content.Headers.ContentLength.ShouldNotBe(0);

        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(CancellationToken);

        document.ShouldNotBeNull();
        document.RootElement.TryGetProperty("status", out var status).ShouldBeTrue();
        status.ValueKind.ShouldBe(JsonValueKind.String);
        status.GetString().ShouldBe("Healthy");
    }
}
