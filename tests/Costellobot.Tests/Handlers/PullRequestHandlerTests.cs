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
        RegisterCommit(driver);

        var pullRequestApproved = RegisterReview(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await pullRequestApproved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Pull_Request_Is_Approved_For_Trusted_User_And_Dependency_Owner()
    {
        // Arrange
        Fixture.ApprovePullRequests();
        await Fixture.Interceptor.RegisterBundleAsync(Path.Combine("Bundles", "nuget-search.json"));

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(TrustedCommitMessage("Newtonsoft.Json", "13.0.1"));

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

        var automergeEnabled = RegisterEnableAutomerge(driver, (p, tcs) => p.WithInterceptionCallback(async (request) =>
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
                tcs.SetResult();
            }

            return hasCorrectPayload;
        }));

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await automergeEnabled.Task.WaitAsync(TimeSpan.FromSeconds(1));
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
        RegisterCommit(driver);
        RegisterReview(driver);

        var pullRequestApproved = RegisterReview(driver);
        var automergeEnabled = RegisterEnableAutomerge(driver);

        // Act
        using var response = await PostWebhookAsync(driver, "labeled");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await pullRequestApproved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await automergeEnabled.Task.WaitAsync(TimeSpan.FromSeconds(1));
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
        RegisterCommit(driver);
        RegisterReview(driver);

        var pullRequestMerged = RegisterPutPullRequestMerge(driver, mergeable: true);
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

        await pullRequestApproved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await automergeEnabled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await pullRequestMerged.Task.WaitAsync(TimeSpan.FromSeconds(1));
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
    public async Task Pull_Request_Is_Not_Approved_For_Trusted_User_And_Dependency_Name_But_Ignored_Repo()
    {
        // Arrange
        Fixture.ApprovePullRequests();

        var driver = PullRequestDriver.ForDependabot()
            .WithCommitMessage(TrustedCommitMessage());

        driver.Repository.Name = "ignored-repo";
        driver.Repository.Owner.Login = "ignored-org";

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
        RegisterCommit(driver);
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
        RegisterCommit(driver);
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
        RegisterCommit(driver);
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
        RegisterCommit(driver);
        RegisterReview(driver);

        var pullRequestMerged = RegisterPutPullRequestMerge(driver, mergeable: false);
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

        await pullRequestApproved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await automergeEnabled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await pullRequestMerged.Task.WaitAsync(TimeSpan.FromSeconds(1));
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

    private void RegisterCollaborator(PullRequestDriver driver, string login, bool isCollaborator)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{driver.Repository.Owner.Login}/{driver.Repository.Name}/collaborators/{login}")
            .Responds()
            .WithStatus(isCollaborator ? HttpStatusCode.NoContent : HttpStatusCode.NotFound)
            .RegisterWith(Fixture.Interceptor);
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

    private TaskCompletionSource RegisterEnableAutomerge(
        PullRequestDriver driver,
        Action<HttpRequestInterceptionBuilder, TaskCompletionSource>? configure = null)
    {
        var data = new
        {
            enablePullRequestAutoMerge = new
            {
                number = new
                {
                    number = driver.PullRequest.Number,
                },
            },
        };

        return RegisterGraphQLQuery(
            (_) => true,
            data,
            configure);
    }

    private TaskCompletionSource RegisterPutPullRequestMerge(PullRequestDriver driver, bool mergeable = true)
    {
        var pullRequestMerged = new TaskCompletionSource();

        CreateDefaultBuilder()
            .Requests()
            .ForPut()
            .ForPath($"/repos/{driver.PullRequest.Repository.Owner.Login}/{driver.PullRequest.Repository.Name}/pulls/{driver.PullRequest.Number}/merge")
            .Responds()
            .WithStatus(mergeable ? StatusCodes.Status200OK : StatusCodes.Status405MethodNotAllowed)
            .WithSystemTextJsonContent(new { merged = mergeable })
            .WithInterceptionCallback((_) => pullRequestMerged.SetResult())
            .RegisterWith(Fixture.Interceptor);

        return pullRequestMerged;
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

    private TaskCompletionSource RegisterGraphQLQuery(
        Predicate<string> queryPredicate,
        object data,
        Action<HttpRequestInterceptionBuilder, TaskCompletionSource>? configure = null)
    {
        var tcs = new TaskCompletionSource();

        var response = new { data };

        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath("graphql")
            .ForContent(async (request) =>
            {
                request.ShouldNotBeNull();

                byte[] body = await request.ReadAsByteArrayAsync();
                using var document = JsonDocument.Parse(body);

                var query = document.RootElement.GetProperty("query").GetString();

                query.ShouldNotBeNull();

                return queryPredicate(query);
            })
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(response)
            .WithInterceptionCallback((_) => tcs.SetResult());

        configure?.Invoke(builder, tcs);

        builder.RegisterWith(Fixture.Interceptor);

        return tcs;
    }
}
