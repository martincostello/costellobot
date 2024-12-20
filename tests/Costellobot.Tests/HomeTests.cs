﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Security.Cryptography;
using MartinCostello.Costellobot.Infrastructure;
using MartinCostello.Costellobot.Pages;
using Microsoft.Playwright;

namespace MartinCostello.Costellobot;

[Collection<HttpServerCollection>]
public class HomeTests(HttpServerFixture fixture, ITestOutputHelper outputHelper) : UITests(fixture, outputHelper)
{
    [Theory]
    [MemberData(nameof(Browsers))]
    public async Task Can_Sign_In_And_Out(string browserType, string? browserChannel)
    {
        // Arrange
        var options = new BrowserFixtureOptions()
        {
            BrowserType = browserType,
            BrowserChannel = browserChannel,
        };

        var browser = new BrowserFixture(options, OutputHelper);
        await browser.WithPageAsync(async page =>
        {
            // Load the application
            await page.GotoAsync(Fixture.ServerAddress);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            var app = new HomePage(page);

            // Act - Sign in
            await app.SignInAsync();

            // Assert
            await app.WaitForSignedInAsync();
            await app.UserNameAsync().ShouldBe("John Smith");

            // Arrange - Wait for the page to be ready
            await app.WaitForContentAsync();

            // Act - Sign out
            await app.SignOutAsync();

            // Assert
            await app.WaitForSignedOutAsync();
        });
    }

    [Theory]
    [MemberData(nameof(Browsers))]
    public async Task Can_View_Logs(string browserType, string? browserChannel)
    {
        // Arrange
        Assert.SkipWhen(
            string.Equals(browserType, BrowserType.Webkit, StringComparison.OrdinalIgnoreCase),
            "Webkit does not trust for self-signed certificate with the web socket.");

        var options = new BrowserFixtureOptions()
        {
            BrowserType = browserType,
            BrowserChannel = browserChannel,
        };

        var browser = new BrowserFixture(options, OutputHelper);
        await browser.WithPageAsync(async page =>
        {
            var connected = new TaskCompletionSource();
            page.WebSocket += (_, p) =>
                p.FrameReceived += (_, _) => connected.TrySetResult();

            // Load the application
            await page.GotoAsync(Fixture.ServerAddress);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            var app = new HomePage(page);

            // Act - Sign in
            await app.SignInAsync();

            // Assert
            await app.WaitForSignedInAsync();
            await app.UserNameAsync().ShouldBe("John Smith");

            // Arrange - Wait for the page to be ready
            await app.WaitForContentAsync();

            // Wait for the web socket to have connected
            await connected.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

            // Act - Deliver a ping webhook
            var value = new
            {
                zen = "You tried your best and you failed miserably. The lesson is, never try.",
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
                    url = "https://costellobot.martincostello.local/github-webhook",
                },
                updated_at = "2022-03-23T23:13:43Z",
                created_at = "2022-03-23T23:13:43Z",
                app_id = 349596565,
                deliveries_url = "https://api.github.com/app/hook/deliveries",
                installation = new
                {
                    id = 42,
                },
            };

            string delivery = RandomNumberGenerator.GetInt32(int.MaxValue).ToString(CultureInfo.InvariantCulture);
            string eventName = "ping";

            using var response = await PostWebhookAsync(eventName, value, delivery: delivery);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            // Assert - Verify log entries were written
            await app.WaitForLogsTextAsync($"Processed webhook with ID {delivery} for event {eventName}.");

            // Act - Get the log entry for the webhook
            var item = await app.WaitForWebhookAsync(delivery);

            // Assert - Verify the properties of the webhook
            await item.DeliveryAsync().ShouldBe(delivery);
            await item.EventAsync().ShouldBe("ping");

            // Act - View the payload
            var content = await item.SelectAsync();

            // Assert - Verify the payload
            string payload = await content.ContentAsync();

            payload.ShouldContain($@"""X-GitHub-Delivery"": ""{delivery}"",");
            payload.ShouldContain($@"""X-GitHub-Event"": ""{eventName}"",");
            payload.ShouldContain(@"""hook_id"": 109948940,");
        });
    }
}
