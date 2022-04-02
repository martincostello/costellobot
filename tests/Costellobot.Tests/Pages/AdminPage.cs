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

    private sealed class Selectors
    {
        internal const string AdminContent = "id=admin-content";
        internal const string Logs = "id=logs";
    }
}
