// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.Costellobot.Pages;

public sealed class ConfigurationPage(IPage page) : AppPage(page)
{
    public async Task WaitForContentAsync()
        => await Page.WaitForSelectorAsync(Selectors.ConfigurationContent);

    private sealed class Selectors
    {
        internal const string ConfigurationContent = "id=configuration-content";
    }
}
