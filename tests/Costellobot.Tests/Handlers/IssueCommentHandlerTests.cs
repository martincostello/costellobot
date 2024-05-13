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
public class IssueCommentHandlerTests(AppFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<AppFixture>(fixture, outputHelper)
{
    [Fact]
    public async Task Repository_Dispatch_Is_Created_For_Rebase()
    {
        // Arrange
        var driver = new IssueCommentDriver("@costellobot rebase", "OWNER");
        driver.Issue.CreatePullRequest();

        RegisterGetAccessToken();
        RegisterPullRequest(driver);

        var dispatched = RegisterDispatch(driver);

        // Act
        using var response = await PostWebhookAsync("created", driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("deleted", "@costellobot rebase", "OWNER", true)]
    [InlineData("edited", "@costellobot rebase", "OWNER", true)]
    [InlineData("created", "whatever", "OWNER", true)]
    [InlineData("created", "@costellobot whatever", "OWNER", true)]
    [InlineData("created", "@costellobot rebase", "COLLABORATOR", true)]
    [InlineData("created", "@costellobot rebase", "CONTRIBUTOR", true)]
    [InlineData("created", "@costellobot rebase", "FIRST_TIMER", true)]
    [InlineData("created", "@costellobot rebase", "FIRST_TIME_CONTRIBUTOR", true)]
    [InlineData("created", "@costellobot rebase", "MANNEQUIN", true)]
    [InlineData("created", "@costellobot rebase", "MEMBER", true)]
    [InlineData("created", "@costellobot rebase", "NONE", true)]
    [InlineData("created", "@costellobot rebase", "OWNER", false)]
    public async Task Repository_Dispatch_Is_Not_Created(
        string action,
        string body,
        string authorAssociation,
        bool isPullRequest)
    {
        // Arrange
        var driver = new IssueCommentDriver(body, authorAssociation);

        if (isPullRequest)
        {
            driver.Issue.CreatePullRequest();
        }

        RegisterGetAccessToken();

        var dispatched = RegisterDispatch(driver);

        // Act
        using var response = await PostWebhookAsync(action, driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(dispatched);
    }

    [Fact]
    public async Task Handler_Ignores_Events_That_Are_Not_Issue_Comments()
    {
        // Arrange
        var target = Fixture.Services.GetRequiredService<IssueCommentHandler>();
        var message = new Octokit.Webhooks.Events.PullRequest.PullRequestOpenedEvent();

        // Act
        await Should.NotThrowAsync(() => target.HandleAsync(message));
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(string action, IssueCommentDriver driver)
    {
        var value = driver.CreateWebhook(action);
        return await PostWebhookAsync("issue_comment", value);
    }

    private void RegisterPullRequest(IssueCommentDriver driver)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{driver.Repository.Owner.Login}/{driver.Repository.Name}/pulls/{driver.Issue.PullRequest!.Number}")
            .Responds()
            .WithJsonContent(driver.Issue.PullRequest.Build())
            .RegisterWith(Fixture.Interceptor);
    }

    private TaskCompletionSource RegisterDispatch(IssueCommentDriver driver)
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

                if (!string.Equals(eventType, "rebase_pull_request", StringComparison.Ordinal))
                {
                    return false;
                }

                var clientPayload = document.RootElement.GetProperty("client_payload");

                if (clientPayload.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                var repository = clientPayload.GetProperty("repository").GetString();
                var baseRef = clientPayload.GetProperty("base").GetString();
                var headRef = clientPayload.GetProperty("head").GetString();
                var number = clientPayload.GetProperty("number").GetInt32();

                return
                    string.Equals(repository, $"{driver.Owner.Login}/{driver.Repository.Name}", StringComparison.Ordinal) &&
                    string.Equals(baseRef, driver.Issue.PullRequest?.RefBase, StringComparison.Ordinal) &&
                    string.Equals(headRef, driver.Issue.PullRequest?.RefHead, StringComparison.Ordinal) &&
                    number == driver.Issue.Number;
            })
            .Responds()
            .WithStatus(HttpStatusCode.NoContent)
            .WithInterceptionCallback((_) => dispatched.SetResult())
            .RegisterWith(Fixture.Interceptor);

        return dispatched;
    }
}
