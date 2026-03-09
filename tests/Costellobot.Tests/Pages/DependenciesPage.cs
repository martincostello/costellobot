// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.Costellobot.Pages;

public sealed class DependenciesPage(IPage page) : AppPage(page)
{
    public async Task<IReadOnlyList<DependencyItem>> GetTrustedDependenciesAsync()
    {
        var elements = await Page.QuerySelectorAllAsync(Selectors.DependencyItem);
        return [.. elements.Select((p) => new DependencyItem(p, Page))];
    }

    public async Task<IReadOnlyList<DeniedDependencyItem>> GetDeniedDependenciesAsync()
    {
        var elements = await Page.QuerySelectorAllAsync(Selectors.DeniedDependencyItem);
        return [.. elements.Select((p) => new DeniedDependencyItem(p, Page))];
    }

    public async Task<DependenciesPage> DistrustAllAsync()
    {
        var waitForResponse = Page.WaitForResponseAsync(IsDependenciesPageResponse);

        await Page.ClickAsync(Selectors.DistrustAll);
        await waitForResponse;

        return new DependenciesPage(Page);
    }

    public async Task<DependenciesPage> DenyDependencyAsync(DependencyEcosystem ecosystem, string id, string version)
    {
        await Page.SelectOptionAsync(Selectors.DenyEcosystem, ecosystem.ToString());
        await Page.FillAsync(Selectors.DenyId, id);
        await Page.FillAsync(Selectors.DenyVersion, version);

        var waitForResponse = Page.WaitForResponseAsync(IsDependenciesPageResponse);

        await Page.ClickAsync(Selectors.DenySubmit);
        await waitForResponse;

        return new DependenciesPage(Page);
    }

    public async Task WaitForContentAsync()
        => await Page.WaitForSelectorAsync(Selectors.DependenciesContent);

    public async Task WaitForTrustedDependenciesCountAsync(int count)
    {
        await Assertions.Expect(Page.Locator(Selectors.DependencyItem))
                        .ToHaveCountAsync(count);
    }

    public async Task WaitForDeniedDependenciesCountAsync(int count)
    {
        await Assertions.Expect(Page.Locator(Selectors.DeniedDependencyItem))
                        .ToHaveCountAsync(count);
    }

    private static bool IsDependenciesPageResponse(IResponse response) =>
        response.Url.EndsWith("/dependencies", StringComparison.Ordinal) &&
        response.Request.Method == "GET" &&
        response.Status == 200;

    public sealed class DependencyItem : Item
    {
        internal DependencyItem(IElementHandle handle, IPage page)
            : base(handle, page)
        {
        }

        public async Task<string> EcosystemAsync()
        {
            var element = await SelectAsync(Selectors.DependencyEcosystem);
            return await element.GetAttributeAsync("title") ?? string.Empty;
        }

        public async Task<string> IdAsync()
            => await StringAsync(Selectors.DependencyId);

        public async Task<string> VersionAsync()
            => await StringAsync(Selectors.DependencyVersion);

        public async Task<DependenciesPage> DistrustAsync()
        {
            var element = await SelectAsync(Selectors.DistrustDependency);

            var waitForResponse = Page.WaitForResponseAsync(IsDependenciesPageResponse);

            await element.ClickAsync();
            await waitForResponse;

            return new DependenciesPage(Page);
        }
    }

    public sealed class DeniedDependencyItem : Item
    {
        internal DeniedDependencyItem(IElementHandle handle, IPage page)
            : base(handle, page)
        {
        }

        public async Task<string> EcosystemAsync()
        {
            var element = await SelectAsync(Selectors.DependencyEcosystem);
            return await element.GetAttributeAsync("title") ?? string.Empty;
        }

        public async Task<string> IdAsync()
            => await StringAsync(Selectors.DependencyId);

        public async Task<string> VersionAsync()
            => await StringAsync(Selectors.DependencyVersion);

        public async Task<DependenciesPage> AllowAsync()
        {
            var element = await SelectAsync(Selectors.AllowDependency);
            await element.ClickAsync();

            var page = new DependenciesPage(Page);
            await page.WaitForContentAsync();

            return page;
        }
    }

    private sealed class Selectors
    {
        internal const string AllowDependency = "[class*='allow-dependency']";
        internal const string DependenciesContent = "id=dependencies-content";
        internal const string DeniedDependencyItem = "[class*='denied-dependency']";
        internal const string DenyEcosystem = "id=deny-ecosystem";
        internal const string DenyId = "id=deny-id";
        internal const string DenySubmit = "id=deny-dependency";
        internal const string DenyVersion = "id=deny-version";
        internal const string DependencyEcosystem = "[class*='dependency-ecosystem']";
        internal const string DependencyId = "[class*='dependency-id']";
        internal const string DependencyItem = "[class*='trusted-dependency']";
        internal const string DependencyVersion = "[class*='dependency-version']";
        internal const string DistrustAll = "id=distrust-all";
        internal const string DistrustDependency = "[class*='distrust-dependency']";
    }
}
