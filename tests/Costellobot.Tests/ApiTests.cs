﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot;

[Collection(HttpServerCollection.Name)]
public sealed class ApiTests(HttpServerFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<HttpServerFixture>(fixture, outputHelper)
{
    [Fact]
    public async Task Can_Accept_GitHub_Check_Suite_Webhook()
    {
        // Arrange
        // See https://docs.github.com/en/developers/webhooks-and-events/webhooks/webhook-events-and-payloads#check_suite
        var value = new
        {
            action = "completed",
            check_suite = new
            {
                status = "completed",
                conclusion = "success",
            },
            installation = new
            {
                id = 42,
            },
        };

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Can_Accept_GitHub_Ping_Webhook()
    {
        // Arrange
        var options = Fixture.Services.GetRequiredService<IOptions<GitHubOptions>>().Value;

        // See https://docs.github.com/en/developers/webhooks-and-events/webhooks/webhook-events-and-payloads#webhook-payload-example-27
        var value = new
        {
            zen = "Responsive is better than fast.",
            hook_id = 109948940,
            hook = new
            {
                type = "App",
                id = 109948940,
                name = "web",
                active = true,
                events = new[] { "*" },
            },
            config = new
            {
                content_type = "json",
                insecure_ssl = "0",
                secret = options.WebhookSecret,
                url = "https://costellobot.martincostello.local/github-webhook",
            },
            updated_at = "2022-03-23T23:13:43Z",
            created_at = "2022-03-23T23:13:43Z",
            app_id = 349596565,
            deliveries_url = "https://api.github.com/app/hook/deliveries",
        };

        // Act
        using var response = await PostWebhookAsync("ping", value, options.WebhookSecret);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Can_Accept_GitHub_Installation_Webhook()
    {
        // Arrange
        // See https://docs.github.com/en/developers/webhooks-and-events/webhooks/webhook-events-and-payloads#installation
        var value = new
        {
            action = "created",
            installation = new
            {
                id = 42,
            },
        };

        // Act
        using var response = await PostWebhookAsync("installation", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Can_Get_Version()
    {
        // Arrange
        using var client = Fixture.CreateHttpClientForApp();

        // Act
        var actual = await client.GetFromJsonAsync<JsonElement>("/version", CancellationToken);

        // Assert
        actual.TryGetProperty("application", out var application).ShouldBeTrue();
        application.TryGetProperty("version", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("GET", "application/json", null)]
    [InlineData("POST", "application/json", null)]
    [InlineData("POST", null, "application/json")]
    public async Task Invalid_Request_Responds_With_Json(
        string method,
        string? accept,
        string? contentType)
    {
        // Arrange
        using var client = Fixture.CreateHttpClientForApp();
        using var request = new HttpRequestMessage(new(method), "/foo");

        if (accept is not null)
        {
            request.Headers.Add("Accept", accept);
        }

        if (contentType is not null)
        {
            request.Content = new StringContent("{}", null, contentType);
        }

        // Act
        using var actual = await client.SendAsync(request, CancellationToken);

        // Assert
        actual.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        actual.Content.Headers.ContentType.ShouldNotBeNull();
        actual.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");

        var response = await actual.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);
        response.ShouldNotBeNull();
        response.Status.ShouldBe(StatusCodes.Status404NotFound);
    }
}
