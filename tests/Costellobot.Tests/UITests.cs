// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Infrastructure;
using MartinCostello.Costellobot.Pages;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace MartinCostello.Costellobot;

[Category("UI")]
[Collection<HttpServerCollection>]
public abstract class UITests(HttpServerFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<HttpServerFixture>(fixture, outputHelper)
{
    public static TheoryData<string, string?> Browsers()
    {
        var browsers = new TheoryData<string, string?>()
        {
            { BrowserType.Chromium, null },
            { BrowserType.Chromium, "chrome" },
            { BrowserType.Firefox, null },
        };

        // Skip on macOS. See https://github.com/microsoft/playwright-dotnet/issues/2920.
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            browsers.Add(BrowserType.Chromium, "msedge");
        }

        if (OperatingSystem.IsMacOS())
        {
            browsers.Add(BrowserType.Webkit, null);
        }

        return browsers;
    }

    public override async ValueTask InitializeAsync()
    {
        InstallPlaywright();
        await base.InitializeAsync();
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        await Fixture.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("all");
        await base.DisposeAsync(disposing);
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
        int exitCode = Microsoft.Playwright.Program.Main(["install"]);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright exited with code {exitCode}");
        }
    }
}
