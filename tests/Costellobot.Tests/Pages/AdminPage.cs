// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.Costellobot.Pages;

public class AdminPage : AppPage
{
    public AdminPage(IPage page)
        : base(page)
    {
    }

    public async Task WaitForContentAsync()
        => await Page.WaitForSelectorAsync(Selectors.AdminContent);

    public async Task WaitForLogsTextAsync(string value)
        => await Assertions.Expect(Page.Locator(Selectors.Logs))
                           .ToContainTextAsync(value);

    public async Task<WebhookItem> WaitForWebhookAsync(string delivery)
    {
        var element = await Page.WaitForSelectorAsync($".webhook-item[x-github-delivery='{delivery}']");
        element.ShouldNotBeNull();

        return new(delivery, element, Page);
    }

    public sealed class WebhookItem : Item
    {
        private readonly string _delivery;

        internal WebhookItem(string delivery, IElementHandle handle, IPage page)
            : base(handle, page)
        {
            _delivery = delivery;
        }

        public async Task<string> DeliveryAsync()
        {
            var element = await Handle.QuerySelectorAsync(".x-github-delivery");
            element.ShouldNotBeNull();

            string? value = await element.TextContentAsync();
            value.ShouldNotBeNull();

            return value;
        }

        public async Task<string> EventAsync()
        {
            var element = await Handle.QuerySelectorAsync(".x-github-event");
            element.ShouldNotBeNull();

            string? value = await element.TextContentAsync();
            value.ShouldNotBeNull();

            return value;
        }

        public async Task<WebhookContent> SelectAsync()
        {
            var element = await Page.WaitForSelectorAsync($".webhook-item[x-github-delivery='{_delivery}']");
            element.ShouldNotBeNull();

            await element.IsVisibleAsync().ShouldBeTrue();
            await element.ClickAsync();

            return new(_delivery, element, Page);
        }
    }

    public sealed class WebhookContent : Item
    {
        private readonly string _delivery;

        internal WebhookContent(string delivery, IElementHandle handle, IPage page)
            : base(handle, page)
        {
            _delivery = delivery;
        }

        public async Task<string> ContentAsync()
        {
            var element = await Page.QuerySelectorAsync($".webhook-content[x-github-delivery='{_delivery}']");
            element.ShouldNotBeNull();

            string? value = await element.TextContentAsync();
            value.ShouldNotBeNull();

            return value;
        }
    }

    private sealed class Selectors
    {
        internal const string AdminContent = "id=admin-content";
        internal const string Logs = "id=logs";
    }
}
