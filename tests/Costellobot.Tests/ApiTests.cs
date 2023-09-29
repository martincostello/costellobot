// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot;

[Collection(AppCollection.Name)]
public sealed class ApiTests(AppFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<AppFixture>(fixture, outputHelper)
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
        using var client = Fixture.CreateClient();

        // Act
        var actual = await client.GetFromJsonAsync<JsonElement>("/version");

        // Assert
        actual.TryGetProperty("version", out _).ShouldBeTrue();
    }
}
