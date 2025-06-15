﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Drivers;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Handlers;

[Collection<AppCollection>]
public class PullRequestHandlerTests(AppFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<AppFixture>(fixture, outputHelper)
{
    [Fact]
    public async Task Pull_Request_Is_Approved_For_Trusted_User_And_Dependency_Name()
    {
        // Arrange
        Fixture.ApprovePullRequests();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(TrustedCommitMessage());

        RegisterGetAccessToken();
        RegisterCommitAndDiff(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await pullRequestApproved.Task.WaitAsync(ResultTimeout, CancellationToken);
    }

    [Fact]
    public async Task Pull_Request_Is_Approved_For_Trusted_User_And_Dependency_Owner()
    {
        // Arrange
        Fixture.ApprovePullRequests();
        await Fixture.Interceptor.RegisterBundleAsync(Path.Combine("Bundles", "nuget-search.json"), cancellationToken: CancellationToken);

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(TrustedCommitMessage("Newtonsoft.Json", "13.0.1"));

        RegisterGetAccessToken();
        RegisterCommitAndDiff(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await pullRequestApproved.Task.WaitAsync(ResultTimeout, CancellationToken);
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
        RegisterCommitAndDiff(driver);
        RegisterReview(driver);

        var automergeEnabled = RegisterEnableAutomerge(driver, (p, tcs) => p.WithInterceptionCallback(async (request) =>
        {
            request.Content.ShouldNotBeNull();

            byte[] body = await request.Content.ReadAsByteArrayAsync(CancellationToken);
            using var document = JsonDocument.Parse(body);

            var query = document.RootElement.GetProperty("query").GetString();

            query.ShouldNotBeNull();

            bool hasCorrectPayload =
                query.Contains(@$"pullRequestId:""{driver.PullRequest.NodeId}""", StringComparison.Ordinal) &&
                query.Contains($"mergeMethod:{mergeMethod}", StringComparison.Ordinal);

            if (hasCorrectPayload)
            {
                tcs.SetResult();
            }

            return hasCorrectPayload;
        }));

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await automergeEnabled.Task.WaitAsync(ResultTimeout, CancellationToken);
    }

    [Theory]
    [InlineData("dependencies")]
    [InlineData("merge-approved")]
    public async Task Pull_Request_Is_Approved_And_Automerge_Is_Enabled_For_Trusted_User_With_Untusted_Dependency_When_Labelled_By_Collaborator(
        string label)
    {
        // Arrange
        Fixture.ApprovePullRequests();
        Fixture.AutoMergeEnabled();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(UntrustedCommitMessage());

        driver.Label = new(label);
        driver.PullRequest.WithLabel("dependencies");
        driver.PullRequest.WithLabel("merge-approved");
        driver.PullRequest.WithLabel(".NET");
        driver.Sender = new("repo-admin");

        RegisterGetAccessToken();
        RegisterCollaborator(driver, driver.Sender.Login, isCollaborator: true);
        RegisterCommitAndDiff(driver);
        RegisterReview(driver);

        var pullRequestApproved = RegisterReview(driver);
        var automergeEnabled = RegisterEnableAutomerge(driver);

        // Act
        using var response = await PostWebhookAsync(driver, "labeled");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await pullRequestApproved.Task.WaitAsync(ResultTimeout, CancellationToken);
        await automergeEnabled.Task.WaitAsync(ResultTimeout, CancellationToken);
    }

    [Theory]
    [InlineData("dependencies")]
    [InlineData("merge-approved")]
    public async Task Pull_Request_Is_Approved_And_Merged_For_Trusted_User_With_Untusted_Dependency_When_Labelled_By_Collaborator(
        string label)
    {
        // Arrange
        Fixture.ApprovePullRequests();
        Fixture.AutoMergeEnabled();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(UntrustedCommitMessage());

        driver.Label = new(label);
        driver.PullRequest.WithLabel("dependencies");
        driver.PullRequest.WithLabel("merge-approved");
        driver.PullRequest.WithLabel(".NET");
        driver.Sender = new("repo-admin");

        RegisterGetAccessToken();
        RegisterCollaborator(driver, driver.Sender.Login, isCollaborator: true);
        RegisterCommitAndDiff(driver);
        RegisterReview(driver);

        var pullRequestMerged = RegisterMergePullRequest(driver, mergeable: true);
        var pullRequestApproved = RegisterReview(driver);
        var automergeEnabled = RegisterEnableAutomerge(driver, (p, tcs) =>
        {
            p.Responds()
             .WithStatus(HttpStatusCode.OK)
             .WithJsonContent(new
             {
                 data = new { enablePullRequestAutoMerge = null as object },
                 errors = new[]
                 {
                     new
                     {
                         type = "UNPROCESSABLE",
                         path = new[]
                         {
                             "enablePullRequestAutoMerge",
                         },
                         locations = new[]
                         {
                             new
                             {
                                 line = 1,
                                 column = 21,
                             },
                         },
                         message = "[\"Pull request Pull request is in clean status\"]",
                     },
                 },
             })
             .WithInterceptionCallback((_) => tcs.SetResult());
        });

        // Act
        using var response = await PostWebhookAsync(driver, "labeled");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await pullRequestApproved.Task.WaitAsync(ResultTimeout, CancellationToken);
        await automergeEnabled.Task.WaitAsync(ResultTimeout, CancellationToken);
        await pullRequestMerged.Task.WaitAsync(ResultTimeout, CancellationToken);
    }

    [Fact]
    public async Task Exception_Is_Not_Thrown_If_Enabling_Automerge_Fails()
    {
        // Arrange
        Fixture.AutoMergeEnabled();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(TrustedCommitMessage());

        RegisterGetAccessToken();
        RegisterCommitAndDiff(driver);
        RegisterReview(driver);

        var automergeEnabled = RegisterEnableAutomerge(driver, (p, tcs) =>
        {
            p.Responds()
             .WithStatus(HttpStatusCode.BadRequest)
             .WithInterceptionCallback((_) => tcs.SetResult());
        });

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await automergeEnabled.Task.WaitAsync(ResultTimeout, CancellationToken);
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Trusted_User_But_Untusted_Dependency()
    {
        // Arrange
        Fixture.ApprovePullRequests();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(UntrustedCommitMessage());

        RegisterGetAccessToken();
        RegisterCommitAndDiff(driver);

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
        RegisterCommitAndDiff(driver);

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
        RegisterCommitAndDiff(driver);

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
        RegisterCommitAndDiff(driver);

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
        RegisterCommitAndDiff(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Trusted_User_And_Dependency_Name_But_Ignored_Repo()
    {
        // Arrange
        Fixture.ApprovePullRequests();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(TrustedCommitMessage());

        driver.Repository.Name = "ignored-repo";
        driver.Repository.Owner.Login = "ignored-org";

        RegisterGetAccessToken();
        RegisterCommitAndDiff(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Trusted_User_With_Untusted_Dependency_When_Not_Labelled_By_Collaborator()
    {
        // Arrange
        Fixture.ApprovePullRequests();
        Fixture.AutoMergeEnabled();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(UntrustedCommitMessage());

        driver.Label = new("merge-approved");
        driver.PullRequest.WithLabel("dependencies");
        driver.PullRequest.WithLabel("merge-approved");
        driver.PullRequest.WithLabel(".NET");
        driver.Sender = new("rando-user");

        RegisterGetAccessToken();
        RegisterCollaborator(driver, driver.Sender.Login, isCollaborator: false);
        RegisterCommitAndDiff(driver);
        RegisterReview(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver, "labeled");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Trusted_User_With_Untusted_Dependency_When_Invalid_Label()
    {
        // Arrange
        Fixture.ApprovePullRequests();
        Fixture.AutoMergeEnabled();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(UntrustedCommitMessage());

        driver.Label = new("invalid-label");
        driver.PullRequest.WithLabel("dependencies");
        driver.PullRequest.WithLabel("invalid-label");
        driver.PullRequest.WithLabel(".NET");
        driver.Sender = new("repo-collaborator");

        RegisterGetAccessToken();
        RegisterCollaborator(driver, driver.Sender.Login, isCollaborator: true);
        RegisterCommitAndDiff(driver);
        RegisterReview(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver, "labeled");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Theory]
    [InlineData("dependencies")]
    [InlineData("merge-approved")]
    public async Task Pull_Request_Is_Not_Approved_For_Trusted_User_With_Untusted_Dependency_When_Required_Label_Missing(string label)
    {
        // Arrange
        Fixture.ApprovePullRequests();
        Fixture.AutoMergeEnabled();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(UntrustedCommitMessage());

        driver.Label = new(label);
        driver.PullRequest.WithLabel(label);
        driver.PullRequest.WithLabel(".NET");
        driver.Sender = new("repo-collaborator");

        RegisterGetAccessToken();
        RegisterCollaborator(driver, driver.Sender.Login, isCollaborator: true);
        RegisterCommitAndDiff(driver);
        RegisterReview(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver, "labeled");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(pullRequestApproved);
    }

    [Fact]
    public async Task Exception_Is_Not_Thrown_If_Merge_Fails()
    {
        // Arrange
        Fixture.ApprovePullRequests();
        Fixture.AutoMergeEnabled();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(UntrustedCommitMessage());

        driver.Label = new("dependencies");
        driver.PullRequest.WithLabel("dependencies");
        driver.PullRequest.WithLabel("merge-approved");
        driver.PullRequest.WithLabel(".NET");
        driver.Sender = new("repo-admin");

        RegisterGetAccessToken();
        RegisterCollaborator(driver, driver.Sender.Login, isCollaborator: true);
        RegisterCommitAndDiff(driver);
        RegisterReview(driver);

        var pullRequestMerged = RegisterMergePullRequest(driver, mergeable: false);
        var pullRequestApproved = RegisterReview(driver);
        var automergeEnabled = RegisterEnableAutomerge(driver, (p, tcs) =>
        {
            p.Responds()
             .WithStatus(HttpStatusCode.OK)
             .WithJsonContent(new
             {
                 data = new
                 {
                     enablePullRequestAutoMerge = null as object,
                 },
                 errors = new[]
                 {
                     new
                     {
                         type = "UNPROCESSABLE",
                         path = new[]
                         {
                             "enablePullRequestAutoMerge",
                         },
                         locations = new[]
                         {
                             new
                             {
                                 line = 1,
                                 column = 21,
                             },
                         },
                         message = "[\"Pull request Pull request is in clean status\"]",
                     },
                 },
             })
             .WithInterceptionCallback((_) => tcs.SetResult());
        });

        // Act
        using var response = await PostWebhookAsync(driver, "labeled");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await pullRequestApproved.Task.WaitAsync(ResultTimeout, CancellationToken);
        await automergeEnabled.Task.WaitAsync(ResultTimeout, CancellationToken);
        await pullRequestMerged.Task.WaitAsync(ResultTimeout, CancellationToken);
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

    [Fact]
    public async Task Pull_Request_Is_Approved_For_Trusted_User_And_Dependency_Name_From_Renovate()
    {
        // Arrange
        Fixture.ApprovePullRequests();

        var driver = PullRequestDriver.ForRenovate()
            .WithCommitMessage(TrustedCommitMessageForRenovate());

        RegisterGetAccessToken();
        RegisterCommitAndDiff(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await pullRequestApproved.Task.WaitAsync(ResultTimeout, CancellationToken);
    }

    [Fact]
    public async Task Pull_Request_Is_Approved_For_Trusted_User_And_Dependency_Owner_From_Renovate()
    {
        // Arrange
        Fixture.ApprovePullRequests();
        await Fixture.Interceptor.RegisterBundleAsync(Path.Combine("Bundles", "nuget-search.json"), cancellationToken: CancellationToken);

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(TrustedCommitMessageForRenovate("Newtonsoft.Json", "13.0.1"));

        RegisterGetAccessToken();
        RegisterCommitAndDiff(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await pullRequestApproved.Task.WaitAsync(ResultTimeout, CancellationToken);
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(PullRequestDriver driver, string action = "opened")
    {
        var value = driver.CreateWebhook(action);
        return await PostWebhookAsync("pull_request", value);
    }
}
