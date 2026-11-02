// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Infrastructure;
using MartinCostello.Costellobot.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot;

[Collection<HttpServerCollection>]
public sealed class AccessibilityTests(HttpServerFixture fixture, ITestOutputHelper outputHelper) : UITests(fixture, outputHelper)
{
    private const string TabSelector = ":scope > button[role='tab'], :scope > li > button[role='tab']";

    private static readonly string[] Tags =
    [
        "best-practice",
        "wcag2a",
        "wcag2aa",
        "wcag21a",
        "wcag21aa",
        "wcag22aa",
    ];

    private string AppName { get; } = "costellobot";

    [Theory]
    [InlineData("/")]
    [InlineData("/error?id=404")]
    [InlineData("/error?id=500")]
    public async Task Page_Has_No_Accessibility_Violations_When_Signed_Out(string path)
    {
        // Arrange
        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            await NavigateToAsync(page, path);

            // Act and Assert
            await AssertNoViolationsAsync(page, path);
        });
    }

    [Fact]
    public async Task Home_Page_Has_No_Accessibility_Violations()
    {
        // Arrange
        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            await SignInAsync(page);

            // Act and Assert
            await AssertNoViolationsAsync(page, "/ (signed in)");
        });
    }

    [Fact]
    public async Task Home_Page_Has_No_Accessibility_Violations_With_Webhook()
    {
        // Arrange
        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            page.WebSocket += (_, p) =>
                p.FrameReceived += (_, _) => connected.TrySetResult();

            await SignInAsync(page);

            var app = new HomePage(page);

            await connected.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

            string delivery = Guid.NewGuid().ToString();

            var value = new
            {
                zen = "Keep it logically awesome.",
                hook_id = 109948940,
                hook = new
                {
                    type = "App",
                    id = 109948940,
                    name = "web",
                    active = true,
                    config = new
                    {
                        content_type = "json",
                        insecure_ssl = "0",
                        url = "https://costellobot.martincostello.local/github-webhook",
                    },
                    deliveries_url = "https://api.github.com/app/hook/deliveries",
                    events = new[] { "*" },
                    ping_url = "https://api.github.com/app/hook/109948940/pings",
                    url = "https://api.github.com/app/hook/109948940",
                },
                updated_at = "2022-03-23T23:13:43Z",
                created_at = "2022-03-23T23:13:43Z",
                app_id = AppId,
                installation = new
                {
                    id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
                    node_id = InstallationNodeId,
                },
            };

            using var response = await PostWebhookAsync("ping", value, delivery: delivery);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            await app.WaitForLogsTextAsync($"Processed webhook with ID {delivery} for event ping.");

            var item = await app.WaitForWebhookAsync(delivery);
            await item.SelectAsync();

            // Act and Assert
            await AssertNoViolationsAsync(page, "/ (with logs and webhook)");
        });
    }

    [Fact]
    public async Task Deliveries_Pages_Have_No_Accessibility_Violations()
    {
        // Arrange
        var first = new WebhookDeliveryBuilder("status");
        var second = new WebhookDeliveryBuilder("issues", "opened", long.Parse(InstallationId, CultureInfo.InvariantCulture), 456);
        var third = new WebhookDeliveryBuilder("pull_request", "closed", long.Parse(InstallationId, CultureInfo.InvariantCulture), 21693)
        {
            Status = "Bad Request",
            StatusCode = 400,
        };

        var payload = second.AsPayload();

        payload.RequestHeaders["Accept"] = "*/*";
        payload.RequestHeaders["content-type"] = "application/json";
        payload.RequestHeaders["User-Agent"] = "GitHub-Hookshot/5d19324";
        payload.RequestHeaders["X-GitHub-Delivery"] = second.Guid.ToString();
        payload.RequestHeaders["X-GitHub-Event"] = second.Event;
        payload.ResponseHeaders["Content-Length"] = "0";
        payload.ResponseHeaders["Date"] = "Tue, 02 Aug 2022 07:36:22 GMT";
        payload.RequestPayload = new { action = second.Action, number = 28 };

        RegisterWebhookDeliveriesForApp(third, second, first);
        RegisterWebhookDeliveryForApp(payload);

        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            var app = await SignInAsync(page);

            var deliveries = await app.DeliveriesAsync(AppName);
            await deliveries.WaitForContentAsync();
            await deliveries.WaitForWebhookCountAsync(3);

            // Act and Assert
            await AssertNoViolationsAsync(page, "/deliveries");

            // Arrange
            var items = await deliveries.GetDeliveriesAsync();
            var delivery = await items[1].ViewAsync();

            await delivery.IdAsync().ShouldBe(payload.Id.ToString(CultureInfo.InvariantCulture));

            // Act and Assert
            await AssertNoViolationsAsync(page, "/delivery (request)");

            // Arrange
            await page.ClickAsync("id=response-tab");
            await Assertions.Expect(page.Locator("id=response")).ToBeVisibleAsync();

            // Act and Assert
            await AssertNoViolationsAsync(page, "/delivery (response)");
        });
    }

    [Fact]
    public async Task Dependencies_Page_Has_No_Accessibility_Violations()
    {
        // Arrange
        var trustStore = Fixture.Services.GetRequiredService<ITrustStore>();

        await trustStore.TrustAsync(DependencyEcosystem.Docker, "devcontainers/dotnet", "latest", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.GitHubActions, "DavidAnson/markdownlint-cli2-action", "19.1.0", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.Npm, "@stylistic/eslint-plugin", "4.0.1", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.NuGet, "Verify.XunitV3", "28.10.1", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.NuGet, "Verify.XunitV3", "28.11.0", CancellationToken);
        await trustStore.DenyAsync(DependencyEcosystem.NuGet, "Humanizer.Core", "2.14.1", CancellationToken);

        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            var app = await SignInAsync(page);

            var dependencies = await app.DependenciesAsync();
            await dependencies.WaitForContentAsync();
            await dependencies.WaitForTrustedDependenciesCountAsync(5);
            await dependencies.WaitForDeniedDependenciesCountAsync(1);

            // Act and Assert
            await AssertNoViolationsAsync(page, "/dependencies");
        });
    }

    [Fact]
    public async Task Dependencies_Page_Has_No_Accessibility_Violations_When_Empty()
    {
        // Arrange
        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            var app = await SignInAsync(page);

            var dependencies = await app.DependenciesAsync();
            await dependencies.WaitForContentAsync();
            await dependencies.WaitForTrustedDependenciesCountAsync(0);
            await dependencies.WaitForDeniedDependenciesCountAsync(0);

            // Act and Assert
            await AssertNoViolationsAsync(page, "/dependencies (empty)");
        });
    }

    [Fact]
    public async Task Debug_Page_Has_No_Accessibility_Violations()
    {
        // Arrange
        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            await SignInAsync(page);
            await NavigateToAsync(page, "/github-webhook");

            // Act and Assert
            await AssertNoViolationsAsync(page, "/github-webhook");
        });
    }

    [Fact]
    public async Task Configuration_Page_Has_No_Accessibility_Violations()
    {
        // Arrange
        var inOneHour = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var rateLimits = new
        {
            resources = new
            {
                core = new { limit = 12_500, used = 1, remaining = 12_499, reset = inOneHour },
                graphql = new { limit = 12_500, used = 0, remaining = 12_500, reset = inOneHour },
                search = new { limit = 30, used = 0, remaining = 30, reset = inOneHour },
            },
        };

        CreateDefaultBuilder()
            .Requests()
            .ForPath("/rate_limit")
            .Responds()
            .WithJsonContent(rateLimits)
            .RegisterWith(Fixture.Interceptor);

        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            var app = await SignInAsync(page);

            var configuration = await app.ConfigurationAsync();
            await configuration.WaitForContentAsync();

            // Act and Assert - Hidden tab panes are not scanned, so activate and check each one in turn
            var topLevel = page.Locator("ul[role='tablist']");
            int topLevelCount = await topLevel.Locator(TabSelector).CountAsync();

            topLevelCount.ShouldBeGreaterThan(0);

            int scanned = await AssertTabsHaveNoViolationsAsync(page, topLevel, "/configuration");

            // Nested tabs (e.g. trusted entities and token broker repositories) must have been checked too
            scanned.ShouldBeGreaterThan(topLevelCount);
        });
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/dependencies")]
    [InlineData("/github-webhook")]
    public async Task Page_Has_No_Accessibility_Violations_On_Mobile_With_Navigation_Expanded(string path)
    {
        // Arrange
        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            await SignInAsync(page);
            await page.SetViewportSizeAsync(375, 667);
            await NavigateToAsync(page, path);

            await page.Locator(".navbar-toggler").ClickAsync();
            await page.Locator("#site-navbar.show").WaitForAsync();

            // Act and Assert
            await AssertNoViolationsAsync(page, path + " (mobile)");
        });
    }

    [Fact]
    public async Task Navigation_Toggle_Reports_Its_State()
    {
        // Arrange
        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            await SignInAsync(page);
            await page.SetViewportSizeAsync(375, 667);
            await NavigateToAsync(page, "/");

            var toggler = page.Locator(".navbar-toggler");

            // Act and Assert
            await Assertions.Expect(toggler).ToHaveAttributeAsync("aria-expanded", "false");

            await toggler.ClickAsync();
            await page.Locator("#site-navbar.show").WaitForAsync();

            await Assertions.Expect(toggler).ToHaveAttributeAsync("aria-expanded", "true");
        });
    }

    [Fact]
    public async Task Deliveries_Menu_Has_No_Accessibility_Violations_When_Open()
    {
        // Arrange
        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            await SignInAsync(page);

            // Act
            await page.ClickAsync("id=deliveries-link");
            await page.Locator(".dropdown-menu.show").WaitForAsync();

            // Assert
            await AssertNoViolationsAsync(page, "/ (deliveries menu open)");
        });
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (Fixture.Services.GetRequiredService<ITrustStore>() is InMemoryTrustStore trustStore)
        {
            trustStore.Reset();
        }

        await base.DisposeAsync(disposing);
    }

    private static async Task<int> AssertTabsHaveNoViolationsAsync(IPage page, ILocator tabList, string description)
    {
        var buttons = tabList.Locator(TabSelector);
        int count = await buttons.CountAsync();
        int scanned = 0;

        for (int i = 0; i < count; i++)
        {
            var button = buttons.Nth(i);

            string name = string.Join(' ', (await button.InnerTextAsync()).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            string paneId = (await button.GetAttributeAsync("aria-controls"))!;

            paneId.ShouldNotBeNullOrEmpty();

            await button.ClickAsync();
            await Assertions.Expect(button).ToHaveAttributeAsync("aria-selected", "true");

            // Allow the fade and colour transitions to complete so the final state of the page is scanned
            await page.WaitForFunctionAsync(
                """
                (id) => {
                    const pane = document.getElementById(id);
                    return pane.classList.contains('active') &&
                           pane.classList.contains('show') &&
                           getComputedStyle(pane).opacity === '1' &&
                           document.getAnimations().length === 0;
                }
                """,
                paneId);

            string path = $"{description} > {name}";

            await AssertNoViolationsAsync(page, path);
            scanned++;

            // Check any tabs nested directly within the pane that was just shown
            var pane = page.Locator($"[id='{paneId}']");
            var nested = pane.Locator("[role='tablist']");
            int nestedCount = await nested.CountAsync();

            for (int j = 0; j < nestedCount; j++)
            {
                var tabs = nested.Nth(j);

                bool isDirectChild = await tabs.EvaluateAsync<bool>(
                    "(element, id) => element.closest('[role=tabpanel]')?.id === id",
                    paneId);

                if (isDirectChild)
                {
                    scanned += await AssertTabsHaveNoViolationsAsync(page, tabs, path);
                }
            }
        }

        return scanned;
    }

    private static async Task AssertNoViolationsAsync(IPage page, string description)
    {
        var options = new AxeRunOptions()
        {
            RunOnly = new RunOnlyOptions()
            {
                Type = "tag",
                Values = [.. Tags],
            },
        };

        var results = await page.RunAxe(options);

        if (results.Violations.Length > 0)
        {
            var builder = new StringBuilder()
                .Append("Found ")
                .Append(results.Violations.Length)
                .Append(" accessibility violation(s) on ")
                .Append(description)
                .AppendLine(":");

            foreach (var violation in results.Violations)
            {
                builder
                    .AppendLine()
                    .Append(violation.Impact)
                    .Append(": ")
                    .Append(violation.Id)
                    .Append(" - ")
                    .AppendLine(violation.Help)
                    .Append("  ")
                    .AppendLine(violation.HelpUrl);

                foreach (var node in violation.Nodes)
                {
                    builder
                        .Append("  - ")
                        .AppendLine(node.Target.ToString());
                }
            }

            Assert.Fail(builder.ToString());
        }
    }

    private async Task NavigateToAsync(IPage page, string path)
    {
        string baseAddress = Fixture.ServerAddress.TrimEnd('/');

        await page.GotoAsync(baseAddress + path);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
