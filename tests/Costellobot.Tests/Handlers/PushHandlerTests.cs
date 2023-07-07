// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Drivers;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MartinCostello.Costellobot.Handlers;

[Collection(AppCollection.Name)]
public class PushHandlerTests : IntegrationTests<AppFixture>
{
    public PushHandlerTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    [Theory]
    [InlineData(new string[0], new[] { "global.json" })]
    [InlineData(new[] { "global.json" }, new string[0])]
    public async Task Repository_Dispatch_Is_Created_If_DotNet_Dependency_File_Added_Or_Modified_On_Main_Branch(string[] added, string[] modified)
    {
        // Arrange
        var driver = new PushDriver();
        driver.Commits.Add(new(new("some-person"))
        {
            Added = added,
            Modified = modified,
        });

        RegisterGetAccessToken();

        var dispatched = RegisterDispatch(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(false, "refs/heads/main", false, false, new string[0], new string[0], new[] { "global.json" })]
    [InlineData(false, "refs/heads/some-branch", false, false, new string[0], new[] { "global.json" }, new string[0])]
    [InlineData(false, "refs/heads/main", true, false, new string[0], new[] { "global.json" }, new string[0])]
    [InlineData(false, "refs/heads/main", false, true, new string[0], new[] { "global.json" }, new string[0])]
    [InlineData(false, "refs/heads/main", false, false, new string[0], new[] { "something.json" }, new string[0])]
    [InlineData(false, "refs/heads/main", false, false, new[] { "tests/assets/global.json" }, new string[0], new string[0])]
    [InlineData(false, "refs/heads/main", false, false, new string[0], new[] { "tests/assets/global.json" }, new string[0])]
    [InlineData(true, "refs/heads/main", false, false, new string[0], new[] { "global.json" }, new string[0])]
    public async Task Repository_Dispatch_Is_Not_Created_If_DotNet_Dependency_File_Added_Or_Modified_On_Main_Branch_Of_Source_Repository(
        bool isFork,
        string reference,
        bool created,
        bool deleted,
        string[] added,
        string[] modified,
        string[] removed)
    {
        // Arrange
        var driver = new PushDriver(isFork)
        {
            Ref = reference,
            Created = created,
            Deleted = deleted,
        };

        driver.Commits.Add(new(new("some-person"))
        {
            Added = added,
            Modified = modified,
            Removed = removed,
        });

        RegisterGetAccessToken();

        var dispatched = RegisterDispatch(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(dispatched);
    }

    [Fact]
    public async Task Handler_Ignores_Events_That_Are_Not_Pushes()
    {
        // Arrange
        var target = Fixture.Services.GetRequiredService<PushHandler>();
        var message = new Octokit.Webhooks.Events.IssueComment.IssueCommentCreatedEvent();

        // Act
        await Should.NotThrowAsync(() => target.HandleAsync(message));
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(PushDriver driver)
    {
        var value = driver.CreateWebhook();
        return await PostWebhookAsync("push", value);
    }

    private TaskCompletionSource RegisterDispatch(PushDriver driver)
    {
        var dispatched = new TaskCompletionSource();

        CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/martincostello/github-automation/dispatches")
            .ForContent(async (request) =>
            {
                request.ShouldNotBeNull();

                byte[] body = await request.ReadAsByteArrayAsync();
                using var document = JsonDocument.Parse(body);

                var eventType = document.RootElement.GetProperty("event_type").GetString();

                if (!string.Equals(eventType, "dotnet_dependencies_updated", StringComparison.Ordinal))
                {
                    return false;
                }

                var clientPayload = document.RootElement.GetProperty("client_payload");

                if (clientPayload.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                var repository = clientPayload.GetProperty("repository").GetString();

                return string.Equals(repository, $"{driver.Owner.Login}/{driver.Repository.Name}", StringComparison.Ordinal);
            })
            .Responds()
            .WithStatus(HttpStatusCode.NoContent)
            .WithInterceptionCallback((_) => dispatched.SetResult())
            .RegisterWith(Fixture.Interceptor);

        return dispatched;
    }
}
