// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MartinCostello.Costellobot;

[Collection<HttpServerCollection>]
public class TrustedDependenciesTests(HttpServerFixture fixture, ITestOutputHelper outputHelper) : UITests(fixture, outputHelper)
{
    [Fact]
    public async Task Can_View_And_Manage_Trusted_Dependencies()
    {
        // Arrange
        var trustStore = Fixture.Services.GetRequiredService<ITrustStore>();

        await trustStore.TrustAsync(DependencyEcosystem.GitHubActions, "DavidAnson/markdownlint-cli2-action", "19.1.0", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.Npm, "@stylistic/eslint-plugin", "4.0.1", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.NuGet, "Verify.ImageMagick", "3.5.0", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.NuGet, "Verify.ImageMagick", "3.6.0", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.NuGet, "Verify.Playwright", "3.0.0", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.NuGet, "Verify.XunitV3", "28.10.1", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.NuGet, "Verify.XunitV3", "28.11.0", CancellationToken);

        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            var app = await SignInAsync(page);

            // Act
            var dependencies = await app.DependenciesAsync();

            // Assert
            await dependencies.WaitForContentAsync();
            await dependencies.WaitForDependenciesCountAsync(7);

            var items = await dependencies.GetDependenciesAsync();

            await items[0].EcosystemAsync().ShouldBe("GitHub Actions");
            await items[0].IdAsync().ShouldBe("DavidAnson/markdownlint-cli2-action");
            await items[0].VersionAsync().ShouldBe("19.1.0");

            await items[1].EcosystemAsync().ShouldBe("npm");
            await items[1].IdAsync().ShouldBe("@stylistic/eslint-plugin");
            await items[1].VersionAsync().ShouldBe("4.0.1");

            await items[2].EcosystemAsync().ShouldBe("NuGet");
            await items[2].IdAsync().ShouldBe("Verify.ImageMagick");
            await items[2].VersionAsync().ShouldBe("3.6.0");

            await items[3].EcosystemAsync().ShouldBe("NuGet");
            await items[3].IdAsync().ShouldBe("Verify.ImageMagick");
            await items[3].VersionAsync().ShouldBe("3.5.0");

            await items[4].EcosystemAsync().ShouldBe("NuGet");
            await items[4].IdAsync().ShouldBe("Verify.Playwright");
            await items[4].VersionAsync().ShouldBe("3.0.0");

            await items[5].EcosystemAsync().ShouldBe("NuGet");
            await items[5].IdAsync().ShouldBe("Verify.XunitV3");
            await items[5].VersionAsync().ShouldBe("28.11.0");

            await items[6].EcosystemAsync().ShouldBe("NuGet");
            await items[6].IdAsync().ShouldBe("Verify.XunitV3");
            await items[6].VersionAsync().ShouldBe("28.10.1");

            // Act
            await items[3].DistrustAsync();

            // Assert
            await dependencies.WaitForContentAsync();
            await dependencies.WaitForDependenciesCountAsync(6);

            items = await dependencies.GetDependenciesAsync();

            await items[0].EcosystemAsync().ShouldBe("GitHub Actions");
            await items[0].IdAsync().ShouldBe("DavidAnson/markdownlint-cli2-action");
            await items[0].VersionAsync().ShouldBe("19.1.0");

            await items[1].EcosystemAsync().ShouldBe("npm");
            await items[1].IdAsync().ShouldBe("@stylistic/eslint-plugin");
            await items[1].VersionAsync().ShouldBe("4.0.1");

            await items[2].EcosystemAsync().ShouldBe("NuGet");
            await items[2].IdAsync().ShouldBe("Verify.ImageMagick");
            await items[2].VersionAsync().ShouldBe("3.6.0");

            await items[3].EcosystemAsync().ShouldBe("NuGet");
            await items[3].IdAsync().ShouldBe("Verify.Playwright");
            await items[3].VersionAsync().ShouldBe("3.0.0");

            await items[4].EcosystemAsync().ShouldBe("NuGet");
            await items[4].IdAsync().ShouldBe("Verify.XunitV3");
            await items[4].VersionAsync().ShouldBe("28.11.0");

            await items[5].EcosystemAsync().ShouldBe("NuGet");
            await items[5].IdAsync().ShouldBe("Verify.XunitV3");
            await items[5].VersionAsync().ShouldBe("28.10.1");

            // Act
            await dependencies.DistrustAllAsync();

            // Assert
            await dependencies.WaitForContentAsync();
            await dependencies.WaitForDependenciesCountAsync(0);
        });
    }
}
