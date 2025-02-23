// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.Costellobot.Pages;

public sealed class DependenciesPage(IPage page) : AppPage(page)
{
    public async Task<IReadOnlyList<DependencyItem>> GetDependenciesAsync()
    {
        var elements = await Page.QuerySelectorAllAsync(Selectors.DependencyItem);
        return [.. elements.Select((p) => new DependencyItem(p, Page))];
    }

    public async Task<DependenciesPage> DistrustAllAsync()
    {
        await Page.ClickAsync(Selectors.DistrustAll);

        var page = new DependenciesPage(Page);
        await page.WaitForContentAsync();

        return page;
    }

    public async Task WaitForContentAsync()
        => await Page.WaitForSelectorAsync(Selectors.DependenciesContent);

    public async Task WaitForDependenciesCountAsync(int count)
    {
        await Assertions.Expect(Page.Locator(Selectors.DependencyItem))
                        .ToHaveCountAsync(count);
    }

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
            await element.ClickAsync();

            var page = new DependenciesPage(Page);
            await page.WaitForContentAsync();

            return page;
        }
    }

    private sealed class Selectors
    {
        internal const string DependenciesContent = "id=dependencies-content";
        internal const string DependencyEcosystem = "[class*='dependency-ecosystem']";
        internal const string DependencyId = "[class*='dependency-id']";
        internal const string DependencyItem = "[class*='trusted-dependency']";
        internal const string DependencyVersion = "[class*='dependency-version']";
        internal const string DistrustAll = "id=distrust-all";
        internal const string DistrustDependency = "[class*='distrust-dependency']";
    }
}
