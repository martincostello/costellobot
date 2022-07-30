// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Infrastructure;
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
        Fixture.OverrideConfiguration("Webhook:Approve", bool.TrueString);

        var user = CreateUserForDependabot();
        var pullRequest = CreatePullRequest(user);

        var commit = pullRequest.CreateCommit();
        commit.Message = TrustedCommitMessage();

        var pullRequestApproved = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCommit(commit);

        RegisterPostReview(
            pullRequest,
            (p) => p.WithInterceptionCallback((_) => pullRequestApproved.SetResult()));

        var value = CreateWebhook(pullRequest);

        // Act
        using var response = await PostWebhookAsync("pull_request", value);

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
        Fixture.OverrideConfiguration("Webhook:Automerge", bool.TrueString);

        var user = CreateUserForDependabot();
        var owner = CreateUser();
        var repo = owner.CreateRepository();

        repo.AllowMergeCommit = allowMergeCommit;
        repo.AllowRebaseMerge = allowRebaseMerge;
        repo.AllowSquashMerge = allowSquashMerge;

        var pullRequest = repo.CreatePullRequest(user);

        var commit = pullRequest.CreateCommit();
        commit.Message = TrustedCommitMessage();

        var automergeEnabled = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCommit(commit);
        RegisterPostReview(pullRequest);

        RegisterEnableAutomerge(
            pullRequest,
            (p) => p.WithInterceptionCallback(async (request) =>
            {
                request.Content.ShouldNotBeNull();

                byte[] body = await request.Content.ReadAsByteArrayAsync();
                using var document = JsonDocument.Parse(body);

                var query = document.RootElement.GetProperty("query").GetString();

                query.ShouldNotBeNull();

                bool hasCorrectPayload =
                    query.Contains(@$"pullRequestId:""{pullRequest.NodeId}""", StringComparison.Ordinal) &&
                    query.Contains($"mergeMethod:{mergeMethod}", StringComparison.Ordinal);

                if (hasCorrectPayload)
                {
                    automergeEnabled.SetResult();
                }

                return hasCorrectPayload;
            }));

        var value = CreateWebhook(pullRequest);

        // Act
        using var response = await PostWebhookAsync("pull_request", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await automergeEnabled.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Exception_Is_Not_Thrown_If_Enabling_Automerge_Fails()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:Automerge", bool.TrueString);

        var user = CreateUserForDependabot();
        var pullRequest = CreatePullRequest(user);

        var commit = pullRequest.CreateCommit();
        commit.Message = TrustedCommitMessage();

        var automergeEnabled = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCommit(commit);
        RegisterPostReview(pullRequest);

        RegisterEnableAutomerge(
            pullRequest,
            (p) =>
            {
                p.Responds()
                 .WithStatus(HttpStatusCode.BadRequest)
                 .WithInterceptionCallback((_) => automergeEnabled.SetResult());
            });

        var value = CreateWebhook(pullRequest);

        // Act
        using var response = await PostWebhookAsync("pull_request", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await automergeEnabled.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Trusted_User_But_Untusted_Dependency()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:Approve", bool.TrueString);

        var user = CreateUserForDependabot();
        var pullRequest = CreatePullRequest(user);

        var commit = pullRequest.CreateCommit();
        commit.Message = UntrustedCommitMessage();

        var pullRequestApproved = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCommit(commit);

        RegisterPostReview(
            pullRequest,
            (p) => p.WithInterceptionCallback((_) => pullRequestApproved.SetResult()));

        var value = CreateWebhook(pullRequest);

        // Act
        using var response = await PostWebhookAsync("pull_request", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Untrusted_User()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:Approve", bool.TrueString);

        var user = CreateUser("rando-calrissian");
        var pullRequest = CreatePullRequest(user);

        var commit = pullRequest.CreateCommit();
        commit.Message = TrustedCommitMessage();

        var pullRequestApproved = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCommit(commit);

        RegisterPostReview(
            pullRequest,
            (p) => p.WithInterceptionCallback((_) => pullRequestApproved.SetResult()));

        var value = CreateWebhook(pullRequest);

        // Act
        using var response = await PostWebhookAsync("pull_request", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Trusted_User_With_No_Dependencies_Detected()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:Approve", bool.TrueString);

        var user = CreateUserForDependabot();
        var pullRequest = CreatePullRequest(user);
        var commit = pullRequest.CreateCommit();

        var pullRequestApproved = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCommit(commit);

        RegisterPostReview(
            pullRequest,
            (p) => p.WithInterceptionCallback((_) => pullRequestApproved.SetResult()));

        var value = CreateWebhook(pullRequest);

        // Act
        using var response = await PostWebhookAsync("pull_request", value);

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
        Fixture.OverrideConfiguration("Webhook:Approve", bool.TrueString);

        var user = CreateUserForDependabot();
        var pullRequest = CreatePullRequest(user);

        var commit = pullRequest.CreateCommit();
        commit.Message = TrustedCommitMessage();

        var pullRequestApproved = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCommit(commit);

        RegisterPostReview(
            pullRequest,
            (p) => p.WithInterceptionCallback((_) => pullRequestApproved.SetResult()));

        var value = CreateWebhook(pullRequest, action);

        // Act
        using var response = await PostWebhookAsync("pull_request", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Draft()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:Approve", bool.TrueString);

        var user = CreateUserForDependabot();
        var pullRequest = CreatePullRequest(user);

        pullRequest.IsDraft = true;

        var commit = pullRequest.CreateCommit();
        commit.Message = TrustedCommitMessage();

        var pullRequestApproved = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCommit(commit);

        RegisterPostReview(
            pullRequest,
            (p) => p.WithInterceptionCallback((_) => pullRequestApproved.SetResult()));

        var value = CreateWebhook(pullRequest);

        // Act
        using var response = await PostWebhookAsync("pull_request", value);

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

    private static PullRequestBuilder CreatePullRequest(UserBuilder user)
    {
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        return repository.CreatePullRequest(user);
    }

    private static object CreateWebhook(PullRequestBuilder pullRequest, string action = "opened")
    {
        return new
        {
            action,
            number = pullRequest.Number,
            pull_request = pullRequest.Build(),
            repository = pullRequest.Repository.Build(),
            installation = new
            {
                id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
            },
        };
    }
}
