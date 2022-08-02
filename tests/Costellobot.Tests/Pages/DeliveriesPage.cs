// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.Costellobot.Pages;

public sealed class DeliveriesPage : AppPage
{
    public DeliveriesPage(IPage page)
        : base(page)
    {
    }

    public async Task<IReadOnlyList<DeliveryItem>> GetDeliveriesAsync()
    {
        var elements = await Page.QuerySelectorAllAsync(Selectors.DeliveryItem);
        return elements.Select((p) => new DeliveryItem(p, Page)).ToArray();
    }

    public async Task<DeliveryPage> FindDeliveryAsync(string deliveryId)
    {
        await Page.FillAsync(Selectors.FindDeliveryInput, deliveryId);
        await Page.ClickAsync(Selectors.FindDeliveryButton);

        var page = new DeliveryPage(Page);
        await page.WaitForContentAsync();

        return page;
    }

    public async Task WaitForContentAsync()
        => await Page.WaitForSelectorAsync(Selectors.DeliveriesContent);

    public async Task WaitForWebhookCountAsync(int count)
    {
        await Assertions.Expect(Page.Locator(Selectors.DeliveryItem))
                        .ToHaveCountAsync(count);
    }

    public sealed class DeliveryItem : Item
    {
        internal DeliveryItem(IElementHandle handle, IPage page)
            : base(handle, page)
        {
        }

        public async Task<string> ActionAsync()
            => await StringAsync(Selectors.DeliveryAction);

        public async Task<string> EventAsync()
            => await StringAsync(Selectors.DeliveryEvent);

        public async Task<string> GuidAsync()
            => await StringAsync(Selectors.DeliveryGuid);

        public async Task<string> IdAsync()
            => await StringAsync(Selectors.DeliveryId);

        public async Task<string> InstallationIdAsync()
            => await StringAsync(Selectors.DeliveryInstallationId);

        public async Task<string> RepositoryIdAsync()
            => await StringAsync(Selectors.DeliveryRepositoryId);

        public async Task<DeliveryPage> ViewAsync()
        {
            var element = await SelectAsync(Selectors.DeliveryId);
            await element.ClickAsync();

            var page = new DeliveryPage(Page);
            await page.WaitForContentAsync();

            return page;
        }

        private async Task<IElementHandle> SelectAsync(string selector)
        {
            var element = await Handle.QuerySelectorAsync(selector);
            element.ShouldNotBeNull();
            return element;
        }

        private async Task<string> StringAsync(string selector)
        {
            var element = await SelectAsync(selector);
            return await element.InnerTextAsync();
        }
    }

    private sealed class Selectors
    {
        internal const string DeliveriesContent = "id=deliveries-content";
        internal const string DeliveryAction = "[class*='delivery-action']";
        internal const string DeliveryEvent = "[class*='delivery-event']";
        internal const string DeliveryGuid = "[class*='delivery-guid']";
        internal const string DeliveryId = "[class*='delivery-id']";
        internal const string DeliveryInstallationId = "[class*='delivery-installation-id']";
        internal const string DeliveryRepositoryId = "[class*='delivery-repository-id']";
        internal const string DeliveryItem = "[class*='webhook-delivery']";
        internal const string FindDeliveryInput = "id=delivery-id";
        internal const string FindDeliveryButton = "id=find-delivery";
    }
}
