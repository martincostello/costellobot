// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Drivers;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MartinCostello.Costellobot.Handlers;

[Collection<AppCollection>]
public sealed class RepositoryDispatchHandlerTests : IntegrationTests<AppFixture>
{
    public RepositoryDispatchHandlerTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
        Fixture.ChangeClock(new(2023, 09, 01, 12, 34, 56, TimeSpan.Zero));
        Fixture.Interceptor.RegisterBundle(Path.Join("Bundles", "grafana.json"));
    }

    [Fact]
    public async Task Creates_And_Updates_Annotation()
    {
        // Arrange
        var driver = new RepositoryDispatchDriver();

        driver.ClientPayload = new
        {
            application = "my-application",
            environment = "production",
            repository = driver.Repository.FullName,
            runAttempt = "1",
            runId = "1234567890",
            runNumber = "1234",
            serverUrl = "https://github.com",
            sha = "abcdef1234567890abcdef1234567890abcdef12",
            timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds(),
        };

        // Act
        using var createResponse = await PostWebhookAsync(driver, "deployment_started");

        // Assert
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(ResultTimeout, CancellationToken);

        // Arrange
        driver.ClientPayload = new
        {
            repository = driver.Repository.FullName,
            runAttempt = "1",
            runNumber = "1234",
            timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds(),
        };

        // Act
        using var updateResponse = await PostWebhookAsync(driver, "deployment_completed");

        // Assert
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(ResultTimeout, CancellationToken);

        AssertNoErrorsLogged();
    }

    [Fact]
    public async Task Handler_Ignores_Events_That_Are_Not_Repository_Dispatches()
    {
        // Arrange
        var target = Fixture.Services.GetRequiredService<RepositoryDispatchHandler>();
        var message = new Octokit.Webhooks.Events.IssueComment.IssueCommentCreatedEvent();

        // Act
        await Should.NotThrowAsync(() => target.HandleAsync(message));
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(RepositoryDispatchDriver driver, string action)
    {
        var value = driver.CreateWebhook(action);
        return await PostWebhookAsync("repository_dispatch", value);
    }
}
