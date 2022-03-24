// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot;

[Collection(AppCollection.Name)]
public sealed class ApiTests : IntegrationTests<AppFixture>
{
    public ApiTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    [Fact]
    public async Task Comment_Is_Posted_To_Pull_Request()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:Comment", bool.TrueString);

        var user = CreateUser("martincostello");
        var repository = user.CreateRepository("costellobot");
        var pullRequest = repository.CreatePullRequest();
        var issueComment = CreateIssueComment("costellobot[bot]", "A comment");

        var commentPosted = new TaskCompletionSource();

        RegisterGetAccessToken();

        RegisterIssueComment(
            pullRequest,
            issueComment,
            (p) => p.WithInterceptionCallback((_) => commentPosted.SetResult()));

        var value = new
        {
            action = "opened",
            number = pullRequest.Number,
            pull_request = pullRequest.Build(),
            repository = repository.Build(),
        };

        // Act
        using var response = await PostWebhookAsync("pull_request", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await commentPosted.Task.WaitAsync(TimeSpan.FromSeconds(1));
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

        // Act
        using var response = await PostWebhookAsync("ping", value, options.WebhookSecret);

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

    private (string Payload, string Signature) CreateWebhook(
        object value,
        string? webhookSecret)
    {
        if (webhookSecret is null)
        {
            var options = Fixture.Services.GetRequiredService<IOptions<GitHubOptions>>().Value;
            webhookSecret = options.WebhookSecret;
        }

        string payload = JsonSerializer.Serialize(value);

        // See https://github.com/terrajobst/Terrajobst.GitHubEvents/blob/cb86100c783373e198cefb1ed7e92526a44833b0/src/Terrajobst.GitHubEvents.AspNetCore/GitHubEventsExtensions.cs#L112-L119
        var encoding = Encoding.UTF8;

        byte[] key = encoding.GetBytes(webhookSecret);
        byte[] data = encoding.GetBytes(payload);

        byte[] hash = HMACSHA256.HashData(key, data);
        string hashString = Convert.ToHexString(hash).ToLowerInvariant();

        return (payload, $"sha256={hashString}");
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(
        string @event,
        object value,
        string? webhookSecret = null)
    {
        (string payload, string signature) = CreateWebhook(value, webhookSecret);

        using var client = Fixture.CreateDefaultClient();

        client.DefaultRequestHeaders.Add("Accept", "*/*");
        client.DefaultRequestHeaders.Add("User-Agent", "GitHub-Hookshot/f05835d");
        client.DefaultRequestHeaders.Add("X-GitHub-Delivery", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-GitHub-Event", @event);
        client.DefaultRequestHeaders.Add("X-GitHub-Hook-ID", "109948940");
        client.DefaultRequestHeaders.Add("X-GitHub-Hook-Installation-Target-ID", "github-installation-target-id");
        client.DefaultRequestHeaders.Add("X-GitHub-Hook-Installation-Target-Type", "github-installation-target-type");
        client.DefaultRequestHeaders.Add("X-Hub-Signature", signature);
        client.DefaultRequestHeaders.Add("X-Hub-Signature-256", signature);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Act
        return await client.PostAsync("/github-webhook", content);
    }
}
