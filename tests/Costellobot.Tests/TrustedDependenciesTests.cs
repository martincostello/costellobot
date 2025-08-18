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

        await trustStore.TrustAsync(DependencyEcosystem.Docker, "devcontainers/dotnet", "latest", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.GitHubActions, "DavidAnson/markdownlint-cli2-action", "19.1.0", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.Npm, "@stylistic/eslint-plugin", "4.0.1", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.NuGet, "Verify.ImageMagick", "3.5.0", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.NuGet, "Verify.ImageMagick", "3.6.0", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.NuGet, "Verify.Playwright", "3.0.0", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.NuGet, "Verify.XunitV3", "28.10.1", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.NuGet, "Verify.XunitV3", "28.11.0", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.Pip, "boto3", "1.40.11", CancellationToken);
        await trustStore.TrustAsync(DependencyEcosystem.Ruby, "rack", "3.1.16", CancellationToken);

        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            var app = await SignInAsync(page);

            // Act
            var dependencies = await app.DependenciesAsync();

            var expected = new[]
            {
                ("Docker", "devcontainers/dotnet", "latest"),
                ("GitHub Actions", "DavidAnson/markdownlint-cli2-action", "19.1.0"),
                ("npm", "@stylistic/eslint-plugin", "4.0.1"),
                ("NuGet", "Verify.ImageMagick", "3.6.0"),
                ("NuGet", "Verify.ImageMagick", "3.5.0"),
                ("NuGet", "Verify.Playwright", "3.0.0"),
                ("NuGet", "Verify.XunitV3", "28.11.0"),
                ("NuGet", "Verify.XunitV3", "28.10.1"),
                ("PyPI", "boto3", "1.40.11"),
                ("Ruby", "rack", "3.1.16"),
            };

            // Assert
            await dependencies.WaitForContentAsync();
            await dependencies.WaitForDependenciesCountAsync(expected.Length);

            var items = await dependencies.GetDependenciesAsync();

            foreach ((var index, var item) in expected.Index())
            {
                (var ecosystem, var id, var version) = item;

                await items[index].EcosystemAsync().ShouldBe(ecosystem);
                await items[index].IdAsync().ShouldBe(id);
                await items[index].VersionAsync().ShouldBe(version);
            }

            // Arrange
            expected =
            [
                ("Docker", "devcontainers/dotnet", "latest"),
                ("GitHub Actions", "DavidAnson/markdownlint-cli2-action", "19.1.0"),
                ("npm", "@stylistic/eslint-plugin", "4.0.1"),
                ("NuGet", "Verify.ImageMagick", "3.6.0"),
                ("NuGet", "Verify.Playwright", "3.0.0"),
                ("NuGet", "Verify.XunitV3", "28.11.0"),
                ("NuGet", "Verify.XunitV3", "28.10.1"),
                ("PyPI", "boto3", "1.40.11"),
                ("Ruby", "rack", "3.1.16"),
            ];

            // Act
            await items[4].DistrustAsync();

            // Assert
            await dependencies.WaitForContentAsync();
            await dependencies.WaitForDependenciesCountAsync(expected.Length);

            items = await dependencies.GetDependenciesAsync();

            foreach ((var index, var item) in expected.Index())
            {
                (var ecosystem, var id, var version) = item;

                await items[index].EcosystemAsync().ShouldBe(ecosystem);
                await items[index].IdAsync().ShouldBe(id);
                await items[index].VersionAsync().ShouldBe(version);
            }

            // Act
            await dependencies.DistrustAllAsync();

            // Assert
            await dependencies.WaitForContentAsync();
            await dependencies.WaitForDependenciesCountAsync(0);
        });
    }
}
