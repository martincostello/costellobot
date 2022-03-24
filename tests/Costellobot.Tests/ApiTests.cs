// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Terrajobst.GitHubEvents;

namespace MartinCostello.Costellobot;

[Collection(AppCollection.Name)]
public sealed class ApiTests : IntegrationTests<AppFixture>
{
    public ApiTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    [Fact]
    public async Task Can_Accept_GitHub_Webhook()
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

        (string payload, string signature) = CreateWebhook(value, options.WebhookSecret);

        using var client = Fixture.CreateDefaultClient();

        client.DefaultRequestHeaders.Add("Accept", "*/*");
        client.DefaultRequestHeaders.Add("User-Agent", "GitHub-Hookshot/f05835d");
        client.DefaultRequestHeaders.Add("X-GitHub-Delivery", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-GitHub-Event", "ping");
        client.DefaultRequestHeaders.Add("X-GitHub-Hook-ID", "109948940");
        client.DefaultRequestHeaders.Add("X-GitHub-Hook-Installation-Target-ID", "github-installation-target-id");
        client.DefaultRequestHeaders.Add("X-GitHub-Hook-Installation-Target-Type", "github-installation-target-type");
        client.DefaultRequestHeaders.Add("X-Hub-Signature", signature);
        client.DefaultRequestHeaders.Add("X-Hub-Signature-256", signature);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PostAsync("/github-webhook", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        GitHubEvent? actual = null;

        while (!cts.IsCancellationRequested)
        {
            if (GitHubWebhookDispatcher.Messages.TryDequeue(out actual))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        actual.ShouldNotBeNull();
        actual.HookId.ShouldBe("109948940");
    }

    private static (string Payload, string Signature) CreateWebhook(object value, string webhookSecret)
    {
        string payload = JsonSerializer.Serialize(value);

        // See https://github.com/terrajobst/Terrajobst.GitHubEvents/blob/cb86100c783373e198cefb1ed7e92526a44833b0/src/Terrajobst.GitHubEvents.AspNetCore/GitHubEventsExtensions.cs#L112-L119
        var encoding = Encoding.UTF8;

        byte[] key = encoding.GetBytes(webhookSecret);
        byte[] data = encoding.GetBytes(payload);

        byte[] hash = HMACSHA256.HashData(key, data);
        string hashString = Convert.ToHexString(hash).ToLowerInvariant();

        return (payload, $"sha256={hashString}");
    }
}
