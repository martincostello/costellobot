// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Infrastructure;

namespace MartinCostello.Costellobot;

[Collection<HttpServerCollection>]
public class ConfigurationTests(HttpServerFixture fixture, ITestOutputHelper outputHelper) : UITests(fixture, outputHelper)
{
    [Fact]
    public async Task Can_View_Configuration()
    {
        // Arrange
        var inOneHour = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var rateLimits = new
        {
            resources = new
            {
                core = new
                {
                    limit = 12_500,
                    used = 1,
                    remaining = 12_499,
                    reset = inOneHour,
                },
                graphql = new
                {
                    limit = 12_500,
                    used = 0,
                    remaining = 12_500,
                    reset = inOneHour,
                },
                search = new
                {
                    limit = 30,
                    used = 0,
                    remaining = 30,
                    reset = inOneHour,
                },
            },
            rate = new
            {
                limit = 12_500,
                used = 1,
                remaining = 12_499,
                reset = inOneHour,
            },
        };

        CreateDefaultBuilder()
            .Requests()
            .ForPath("/rate_limit")
            .Responds()
            .WithJsonContent(rateLimits)
            .RegisterWith(Fixture.Interceptor);

        var browser = new BrowserFixture(OutputHelper);
        await browser.WithPageAsync(async page =>
        {
            var app = await SignInAsync(page);

            // Act
            var dependencies = await app.ConfigurationAsync();

            // Assert
            await dependencies.WaitForContentAsync();
        });
    }
}
