// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Drivers;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Handlers;

[Collection(AppCollection.Name)]
public class PullRequestHandlerTests : IntegrationTests<AppFixture>
{
    public PullRequestHandlerTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    [Fact]
    public async Task Pull_Request_Is_Approved_For_Trusted_User_And_Dependency()
    {
        // Arrange
        Fixture.ApprovePullRequests();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(TrustedCommitMessage());

        RegisterGetAccessToken();
        RegisterCommit(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await pullRequestApproved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null, null, null, "MERGE")]
    [InlineData(false, false, false, "MERGE")]
    [InlineData(false, false, true, "SQUASH")]
    [InlineData(false, true, false, "REBASE")]
    [InlineData(false, true, true, "SQUASH")]
    [InlineData(true, false, false, "MERGE")]
    [InlineData(true, true, false, "MERGE")]
    public async Task Pull_Request_Automerge_Is_Enabled_For_Trusted_User_And_Dependency(
        bool? allowMergeCommit,
        bool? allowRebaseMerge,
        bool? allowSquashMerge,
        string mergeMethod)
    {
        // Arrange
        Fixture.AutoMergeEnabled();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(TrustedCommitMessage());

        driver.Repository.AllowMergeCommit = allowMergeCommit;
        driver.Repository.AllowRebaseMerge = allowRebaseMerge;
        driver.Repository.AllowSquashMerge = allowSquashMerge;

        RegisterGetAccessToken();
        RegisterCommit(driver);
        RegisterReview(driver);

        var automergeEnabled = new TaskCompletionSource();
        RegisterEnableAutomerge(driver, (p) => p.WithInterceptionCallback(async (request) =>
        {
            request.Content.ShouldNotBeNull();

            byte[] body = await request.Content.ReadAsByteArrayAsync();
            using var document = JsonDocument.Parse(body);

            var query = document.RootElement.GetProperty("query").GetString();

            query.ShouldNotBeNull();

            bool hasCorrectPayload =
                query.Contains(@$"pullRequestId:""{driver.PullRequest.NodeId}""", StringComparison.Ordinal) &&
                query.Contains($"mergeMethod:{mergeMethod}", StringComparison.Ordinal);

            if (hasCorrectPayload)
            {
                automergeEnabled.SetResult();
            }

            return hasCorrectPayload;
        }));

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await automergeEnabled.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Exception_Is_Not_Thrown_If_Enabling_Automerge_Fails()
    {
        // Arrange
        Fixture.AutoMergeEnabled();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(TrustedCommitMessage());

        RegisterGetAccessToken();
        RegisterCommit(driver);
        RegisterReview(driver);

        var automergeEnabled = new TaskCompletionSource();
        RegisterEnableAutomerge(driver, (p) =>
        {
            p.Responds()
             .WithStatus(HttpStatusCode.BadRequest)
             .WithInterceptionCallback((_) => automergeEnabled.SetResult());
        });

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await automergeEnabled.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Trusted_User_But_Untusted_Dependency()
    {
        // Arrange
        Fixture.ApprovePullRequests();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(UntrustedCommitMessage());

        RegisterGetAccessToken();
        RegisterCommit(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Untrusted_User()
    {
        // Arrange
        Fixture.ApprovePullRequests();

        var driver = new PullRequestDriver("rando-calrissian")
            .WithCommitMessage(TrustedCommitMessage());

        RegisterGetAccessToken();
        RegisterCommit(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Trusted_User_With_No_Dependencies_Detected()
    {
        // Arrange
        Fixture.ApprovePullRequests();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage("Fix a typo");

        RegisterGetAccessToken();
        RegisterCommit(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Theory]
    [InlineData("assigned")]
    [InlineData("auto_merge_disabled")]
    [InlineData("auto_merge_enabled")]
    [InlineData("closed")]
    [InlineData("converted_to_draft")]
    [InlineData("edited")]
    [InlineData("labeled")]
    [InlineData("locked")]
    [InlineData("ready_for_review")]
    [InlineData("reopened")]
    [InlineData("review_request_removed")]
    [InlineData("review_requested")]
    [InlineData("synchronize")]
    [InlineData("unassigned")]
    [InlineData("unlabeled")]
    [InlineData("unlocked")]
    public async Task Pull_Request_Is_Not_Approved_For_Ignored_Action(string action)
    {
        // Arrange
        Fixture.ApprovePullRequests();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(TrustedCommitMessage());

        RegisterGetAccessToken();
        RegisterCommit(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver, action);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Draft()
    {
        // Arrange
        Fixture.ApprovePullRequests();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(TrustedCommitMessage());

        driver.PullRequest.IsDraft = true;

        RegisterGetAccessToken();
        RegisterCommit(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Fact]
    public async Task Handler_Ignores_Events_That_Are_Not_Pull_Requests()
    {
        // Arrange
        var target = Fixture.Services.GetRequiredService<PullRequestHandler>();
        var message = new Octokit.Webhooks.Events.IssueComment.IssueCommentCreatedEvent();

        // Act
        await Should.NotThrowAsync(() => target.HandleAsync(message));
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(PullRequestDriver driver, string action = "opened")
    {
        var value = driver.CreateWebhook(action);
        return await PostWebhookAsync("pull_request", value);
    }

    private void RegisterCommit(PullRequestDriver driver)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{driver.Repository.Owner.Login}/{driver.Repository.Name}/commits/{driver.Commit.Sha}")
            .Responds()
            .WithJsonContent(driver.Commit)
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterEnableAutomerge(
        PullRequestDriver driver,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        var response = new
        {
            data = new
            {
                enablePullRequestAutoMerge = new
                {
                    number = new
                    {
                        number = driver.PullRequest.Number,
                    },
                },
            },
        };

        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath("graphql")
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(response);

        configure?.Invoke(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private TaskCompletionSource RegisterReview(PullRequestDriver driver)
    {
        var pullRequestApproved = new TaskCompletionSource();

        RegisterReview(
            driver,
            (p) => p.WithInterceptionCallback((_) => pullRequestApproved.SetResult()));

        return pullRequestApproved;
    }

    private void RegisterReview(
        PullRequestDriver driver,
        Action<HttpRequestInterceptionBuilder> configure)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{driver.PullRequest.Repository.Owner.Login}/{driver.PullRequest.Repository.Name}/pulls/{driver.PullRequest.Number}/reviews")
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(new { });

        configure(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }
}
