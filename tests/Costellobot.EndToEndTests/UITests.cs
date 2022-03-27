// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.Costellobot;

public class UITests : EndToEndTest, IAsyncLifetime
{
    public UITests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    [SkippableFact]
    public async Task Can_Load_Homepage()
    {
        // Arrange
        using var playwright = await Playwright.CreateAsync();

        var browserType = playwright[BrowserType.Chromium];

        await using var browser = await browserType.LaunchAsync();
        await using var context = await browser.NewContextAsync();

        var page = await context.NewPageAsync();

        // Act
        await page.GotoAsync(Fixture.ServerAddress.ToString());
        await page.WaitForLoadStateAsync();

        // Assert
        await Assertions.Expect(page.Locator("id=sign-in")).ToBeVisibleAsync();
    }

    public Task InitializeAsync()
    {
        int exitCode = Program.Main(new[] { "install" });

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright exited with code {exitCode}.");
        }

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
