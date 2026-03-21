// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;

namespace MartinCostello.Costellobot;

[Collection<HttpServerCollection>]
public sealed class HealthCheckTests(HttpServerFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<HttpServerFixture>(fixture, outputHelper)
{
    private const string ValidToken = "I3eGXFWsxHOxEhULepTolmX9jSd/rcSs9CAjao2DgGk=";

    public static TheoryData<string> HealthCheckUrls() =>
    [
        "/health/liveness",
        "/health/readiness",
        "/health/startup"
    ];

    public static TheoryData<string, string> HealthCheckUrlsWithHeaders()
    {
        var data = new TheoryData<string, string>();

        foreach (var url in HealthCheckUrls())
        {
            data.Add(url, "x-health-probe-token");
            data.Add(url, "x-ms-auth-internal-token");
        }

        return data;
    }

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
    public async Task Can_Get_Health_Resource_Unauthenticated_With_Query_Token(string requestUri)
    {
        // Arrange
        using var client = Fixture.CreateHttpClientForApp();

        requestUri = QueryHelpers.AddQueryString(requestUri, "x-health-probe-token", ValidToken);

        // Act and Assert
        await CanGetHealthResource(client, requestUri);
    }

    [Theory]
    [MemberData(nameof(HealthCheckUrlsWithHeaders))]
    public async Task Can_Get_Health_Resource_Unauthenticated_With_Header_Token(string requestUri, string headerName)
    {
        // Arrange
        using var client = Fixture.CreateHttpClientForApp();

        client.DefaultRequestHeaders.Add(headerName, ValidToken);

        // Act and Assert
        await CanGetHealthResource(client, requestUri);
    }

    [Theory]
    [MemberData(nameof(HealthCheckUrls))]
    public async Task Can_Get_Health_Resource_Authenticated_As_User(string requestUri)
    {
        // Arrange
        using var client = await CreateAuthenticatedClientAsync();

        // Act and Assert
        await CanGetHealthResource(client, requestUri);
    }

    private async Task CanGetHealthResource(HttpClient client, string requestUri)
    {
        // Act
        using var response = await client.GetAsync(requestUri, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Failed to get {requestUri}. {await response.Content.ReadAsStringAsync(CancellationToken)}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe(MediaTypeNames.Application.Json);
        response.Content.Headers.ContentLength.ShouldNotBeNull();
        response.Content.Headers.ContentLength.ShouldNotBe(0);

        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(CancellationToken);

        document.ShouldNotBeNull();
        document.RootElement.TryGetProperty("status", out var status).ShouldBeTrue();
        status.ValueKind.ShouldBe(JsonValueKind.String);
        status.GetString().ShouldBe("Healthy");

        document.RootElement.TryGetProperty("version", out var version).ShouldBeTrue();
        version.ValueKind.ShouldBe(JsonValueKind.String);
        version.GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
