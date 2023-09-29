// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.Costellobot.Pages;

public sealed class DeliveryPage(IPage page) : AppPage(page)
{
    public async Task<string> GuidAsync()
        => await Page.GetAttributeAsync(Selectors.DeliveryContent, "data-delivery-guid") ?? string.Empty;

    public async Task<string> IdAsync()
        => await Page.GetAttributeAsync(Selectors.DeliveryContent, "data-delivery-id") ?? string.Empty;

    public async Task<string> RequestHeadersAsync()
        => await Page.InnerTextAsync(Selectors.DeliveryContent) ?? string.Empty;

    public async Task<string> RequestPayloadAsync()
        => await Page.InnerTextAsync(Selectors.RequestPayload) ?? string.Empty;

    public async Task<DeliveriesPage> RedeliverAsync()
    {
        await Page.ClickAsync(Selectors.RedeliverButton);

        var page = new DeliveriesPage(Page);
        await page.WaitForContentAsync();

        return page;
    }

    public async Task WaitForContentAsync()
        => await Page.WaitForSelectorAsync(Selectors.DeliveryContent);

    private sealed class Selectors
    {
        internal const string DeliveryContent = "id=delivery-content";
        internal const string RedeliverButton = "id=redeliver-payload";
        internal const string RequestHeaders = "id=request-headers";
        internal const string RequestPayload = "id=request-payload";
    }
}
