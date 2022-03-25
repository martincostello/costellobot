// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json;
using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Infrastructure;
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

        var user = CreateUser("dependabot[bot]");
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

    [Fact]
    public async Task Pull_Request_Automerge_Is_Enabled_For_Trusted_User_And_Dependency()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:Automerge", bool.TrueString);

        var user = CreateUser("dependabot[bot]");
        var pullRequest = CreatePullRequest(user);

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

                bool hasCorrectId = query.Contains(@$"pullRequestId:""{pullRequest.NodeId}""", StringComparison.Ordinal);

                if (hasCorrectId)
                {
                    automergeEnabled.SetResult();
                }

                return hasCorrectId;
            }));

        var value = CreateWebhook(pullRequest);

        // Act
        using var response = await PostWebhookAsync("pull_request", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await automergeEnabled.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Pull_Request_Is_Not_Approved_For_Trusted_User_But_Untusted_Dependency()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:Approve", bool.TrueString);

        var user = CreateUser("dependabot[bot]");
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

        var user = CreateUser("dependabot[bot]");
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

        var user = CreateUser("dependabot[bot]");
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

    private static async Task AssertTaskNotRun(TaskCompletionSource source)
    {
        await Task.Delay(TimeSpan.FromSeconds(0.1));
        source.Task.Status.ShouldBe(TaskStatus.WaitingForActivation);
    }

    private static string TrustedCommitMessage() => @"
Bump NodaTimeVersion from 3.0.9 to 3.0.10
Bumps `NodaTimeVersion` from 3.0.9 to 3.0.10.

Updates `NodaTime` from 3.0.9 to 3.0.10
- [Release notes](https://github.com/nodatime/nodatime/releases)
- [Changelog](https://github.com/nodatime/nodatime/blob/master/NodaTime%20Release.snk)
- [Commits](nodatime/nodatime@3.0.9...3.0.10)

Updates `NodaTime.Testing` from 3.0.9 to 3.0.10
- [Release notes](https://github.com/nodatime/nodatime/releases)
- [Changelog](https://github.com/nodatime/nodatime/blob/master/NodaTime%20Release.snk)
- [Commits](nodatime/nodatime@3.0.9...3.0.10)

---
updated-dependencies:
- dependency-name: NodaTime
  dependency-type: direct:production
  update-type: version-update:semver-patch
- dependency-name: NodaTime.Testing
  dependency-type: direct:production
  update-type: version-update:semver-patch
...

Signed-off-by: dependabot[bot] <support@github.com>";

    private static string UntrustedCommitMessage() => @"
Bump puppeteer from 13.5.0 to 13.5.1 in /src/LondonTravel.Site
Bumps [puppeteer](https://github.com/puppeteer/puppeteer) from 13.5.0 to 13.5.1.
- [Release notes](https://github.com/puppeteer/puppeteer/releases)
- [Changelog](https://github.com/puppeteer/puppeteer/blob/main/CHANGELOG.md)
- [Commits](puppeteer/puppeteer@v13.5.0...v13.5.1)

---
updated-dependencies:
- dependency-name: puppeteer
  dependency-type: direct:development
  update-type: version-update:semver-patch
...

Signed-off-by: dependabot[bot] <support@github.com>";
}
