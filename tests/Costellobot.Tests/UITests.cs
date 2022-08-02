// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Infrastructure;
using MartinCostello.Costellobot.Pages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace MartinCostello.Costellobot;

[Collection(HttpServerCollection.Name)]
public abstract class UITests : IntegrationTests<HttpServerFixture>
{
    protected UITests(HttpServerFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    public static IEnumerable<object?[]> Browsers()
    {
        yield return new[] { BrowserType.Chromium, null };
        yield return new[] { BrowserType.Chromium, "chrome" };

        if (!OperatingSystem.IsLinux())
        {
            yield return new[] { BrowserType.Chromium, "msedge" };
        }

        yield return new[] { BrowserType.Firefox, null };

        if (OperatingSystem.IsMacOS())
        {
            yield return new[] { BrowserType.Webkit, null };
        }
    }

    public override async Task InitializeAsync()
    {
        InstallPlaywright();
        await base.InitializeAsync();
    }

    public override Task DisposeAsync()
    {
        var cache = Fixture.Services.GetRequiredService<IMemoryCache>();

        if (cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(100);
        }

        return base.DisposeAsync();
    }

    protected async Task<AppPage> SignInAsync(IPage page, bool waitForContent = true)
    {
        await page.GotoAsync(Fixture.ServerAddress);
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        var app = new HomePage(page);

        await app.SignInAsync();
        await app.WaitForSignedInAsync();

        if (waitForContent)
        {
            await app.WaitForContentAsync();
        }

        return app;
    }

    private static void InstallPlaywright()
    {
        int exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright exited with code {exitCode}");
        }
    }
}
