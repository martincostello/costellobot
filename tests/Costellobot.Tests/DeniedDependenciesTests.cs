// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MartinCostello.Costellobot;

[Collection<HttpServerCollection>]
public class DeniedDependenciesTests(HttpServerFixture fixture, ITestOutputHelper outputHelper) : UITests(fixture, outputHelper)
{
    [Fact]
    public async Task Can_View_Denied_Dependencies()
    {
        // Arrange
        var trustStore = Fixture.Services.GetRequiredService<ITrustStore>();

        await trustStore.DenyAsync(DependencyEcosystem.NuGet, "Humanizer.Core", "2.14.2", CancellationToken);
        await trustStore.DenyAsync(DependencyEcosystem.NuGet, "Humanizer.Core", "2.14.1", CancellationToken);

        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            var app = await SignInAsync(page);

            // Act
            var dependencies = await app.DependenciesAsync();

            var expected = new[]
            {
                ("NuGet", "Humanizer.Core", "2.14.2"),
                ("NuGet", "Humanizer.Core", "2.14.1"),
            };

            // Assert
            await dependencies.WaitForContentAsync();
            await dependencies.WaitForDeniedDependenciesCountAsync(expected.Length);

            var items = await dependencies.GetDeniedDependenciesAsync();

            items.Count.ShouldBe(expected.Length);
        });
    }

    [Fact]
    public async Task Can_Deny_A_Dependency_Via_The_UI()
    {
        // Arrange
        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            var app = await SignInAsync(page);

            // Act
            var dependencies = await app.DependenciesAsync();
            await dependencies.WaitForContentAsync();
            await dependencies.WaitForDeniedDependenciesCountAsync(0);

            // Act - deny a dependency via the form
            dependencies = await dependencies.DenyDependencyAsync(DependencyEcosystem.NuGet, "Humanizer.Core", "2.14.2");

            // Assert
            await dependencies.WaitForDeniedDependenciesCountAsync(1);

            var items = await dependencies.GetDeniedDependenciesAsync();
            items.Count.ShouldBe(1);
            await items[0].EcosystemAsync().ShouldBe("NuGet");
            await items[0].IdAsync().ShouldBe("Humanizer.Core");
            await items[0].VersionAsync().ShouldBe("2.14.2");
        });
    }
}
