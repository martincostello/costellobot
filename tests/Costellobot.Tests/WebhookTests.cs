// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Infrastructure;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot;

[Collection(HttpServerCollection.Name)]
public class WebhookTests : UITests
{
    public WebhookTests(HttpServerFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    [Fact]
    public async Task Can_View_Webhook_Deliveries()
    {
        // Arrange
        var first = new WebhookDeliveryBuilder("status");
        var second = new WebhookDeliveryBuilder("issues", "opened", 123, 456);
        var third = second.AsRedelivery();

        RegisterWebhookDeliveriesForApp(third, second, first);

        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            var app = await SignInAsync(page);

            // Act
            var deliveries = await app.DeliveriesAsync();

            // Assert
            await deliveries.WaitForContentAsync();
            await deliveries.WaitForWebhookCountAsync(3);

            var items = await deliveries.GetDeliveriesAsync();

            await items[0].ActionAsync().ShouldBe("opened");
            await items[0].EventAsync().ShouldBe("issues");
            await items[0].GuidAsync().ShouldBe(third.Guid.ToString());
            await items[0].IdAsync().ShouldBe(third.Id.ToString(CultureInfo.InvariantCulture));
            await items[0].RepositoryIdAsync().ShouldBe("-");

            await items[1].ActionAsync().ShouldBe("opened");
            await items[1].EventAsync().ShouldBe("issues");
            await items[1].GuidAsync().ShouldBe(second.Guid.ToString());
            await items[1].IdAsync().ShouldBe(second.Id.ToString(CultureInfo.InvariantCulture));
            await items[1].RepositoryIdAsync().ShouldBe("456");

            await items[2].ActionAsync().ShouldBe("-");
            await items[2].EventAsync().ShouldBe("status");
            await items[2].GuidAsync().ShouldBe(first.Guid.ToString());
            await items[2].IdAsync().ShouldBe(first.Id.ToString(CultureInfo.InvariantCulture));
            await items[2].RepositoryIdAsync().ShouldBe("-");
        });
    }

    [Fact]
    public async Task Can_Find_Webhook_Delivery()
    {
        // Arrange
        var item = new WebhookDeliveryBuilder("issues", "opened", 123, 456);

        RegisterWebhookDeliveriesForApp(item);

        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            var app = await SignInAsync(page);

            var deliveries = await app.DeliveriesAsync();

            await deliveries.WaitForContentAsync();
            await deliveries.WaitForWebhookCountAsync(1);

            var items = await deliveries.GetDeliveriesAsync();

            var wanted = new WebhookDeliveryBuilder("pull_request", "opened", 123, 456);
            var payload = wanted.AsPayload();

            RegisterWebhookDeliveriesForApp(new[] { item }, null, "v1_abc123");
            RegisterWebhookDeliveriesForApp(new[] { wanted }, "v1_abc123", null);
            RegisterWebhookDeliveryForApp(payload);

            var delivery = await deliveries.FindDeliveryAsync(wanted.Guid.ToString());

            await delivery.IdAsync().ShouldBe(payload.Id.ToString(CultureInfo.InvariantCulture));
            await delivery.GuidAsync().ShouldBe(payload.Guid.ToString());
        });
    }

    [Fact]
    public async Task Can_View_Webhook_Delivery()
    {
        // Arrange
        var delivery = new WebhookDeliveryBuilder("pull_request", "closed", 274, 21693);
        var other1 = new WebhookDeliveryBuilder("status");
        var other2 = new WebhookDeliveryBuilder("status");

        var payload = delivery.AsPayload();

        payload.RequestHeaders["Accept"] = "*/*";
        payload.RequestHeaders["content-type"] = "application/json";
        payload.RequestHeaders["User-Agent"] = "GitHub-Hookshot/5d19324";
        payload.RequestHeaders["X-GitHub-Delivery"] = delivery.Guid.ToString();
        payload.RequestHeaders["X-GitHub-Event"] = delivery.Event;
        payload.RequestHeaders["X-GitHub-Hook-ID"] = "1234";
        payload.RequestHeaders["X-GitHub-Hook-Installation-Target-ID"] = "123";
        payload.RequestHeaders["X-GitHub-Hook-Installation-Target-Type"] = "integration";
        payload.RequestHeaders["X-Hub-Signature"] = "sha1=cb134d106fbcb5159d75ad99c1d583f83e284206";
        payload.RequestHeaders["X-Hub-Signature-256"] = "sha256=923baf7522b4180d17d0d4309ecad4d886a89fe1cc4c53ca5796d57f7ad61227";

        payload.ResponseHeaders["Content-Length"] = "0";
        payload.ResponseHeaders["Date"] = "Tue, 02 Aug 2022 07:36:22 GMT";

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var pull = repo.CreatePullRequest();

        payload.RequestPayload = new
        {
            action = delivery.Action,
            number = 28,
            pull_request = pull.Build(),
            installation = new
            {
                id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
            },
        };

        RegisterWebhookDeliveriesForApp(delivery, other1, other2);
        RegisterWebhookDeliveryForApp(payload);

        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            var app = await SignInAsync(page);

            var deliveries = await app.DeliveriesAsync();

            await deliveries.WaitForContentAsync();
            await deliveries.WaitForWebhookCountAsync(3);

            var items = await deliveries.GetDeliveriesAsync();

            // Act
            var item = await items[0].ViewAsync();

            // Assert
            await item.IdAsync().ShouldBe(payload.Id.ToString(CultureInfo.InvariantCulture));
            await item.GuidAsync().ShouldBe(payload.Guid.ToString());

            await item.RequestHeadersAsync().ShouldNotBeNullOrWhiteSpace();
            await item.RequestPayloadAsync().ShouldNotBeNullOrWhiteSpace();

            var redelivery = delivery.AsRedelivery();

            RegisterWebhookDeliveriesForApp(redelivery, delivery, other1, other2);
            RegisterRedeliverWebhookForApp(redelivery);

            // Act
            deliveries = await item.RedeliverAsync();

            await deliveries.WaitForContentAsync();
            await deliveries.WaitForWebhookCountAsync(4);

            items = await deliveries.GetDeliveriesAsync();

            await items[0].ActionAsync().ShouldBe(delivery.Action ?? string.Empty);
            await items[0].EventAsync().ShouldBe(delivery.Event);
            await items[0].GuidAsync().ShouldBe(delivery.Guid.ToString());
            await items[0].IdAsync().ShouldBe(delivery.Id.ToString(CultureInfo.InvariantCulture));
            await items[0].RepositoryIdAsync().ShouldBe("-");
        });
    }

    private void RegisterWebhookDeliveriesForApp(params WebhookDeliveryBuilder[] deliveries)
        => RegisterWebhookDeliveriesForApp(deliveries, null, null);

    private void RegisterWebhookDeliveriesForApp(
        IList<WebhookDeliveryBuilder> deliveries,
        string? cursor = null,
        string? nextCursor = null)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPath("/app/hook/deliveries")
            .ForQuery($"per_page=100{(cursor is null ? string.Empty : $"&cursor={cursor}")}")
            .Responds()
            .WithJsonContent(deliveries.Build());

        if (nextCursor is not null)
        {
            builder.WithResponseHeader("Link", $"<https://api.github.com/app/hook/deliveries?per_page=100&cursor={nextCursor}>; rel=\"next\"");
        }

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterWebhookDeliveryForApp(WebhookPayloadBuilder payload)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/app/hook/deliveries/{payload.Id}")
            .Responds()
            .WithJsonContent(payload)
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterRedeliverWebhookForApp(WebhookDeliveryBuilder payload)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/app/hook/deliveries/{payload.Id}/attempts")
            .Responds()
            .WithJsonContent(payload)
            .RegisterWith(Fixture.Interceptor);
    }
}
