// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Handlers;

[Collection(AppCollection.Name)]
public class CheckSuiteHandlerTests : IntegrationTests<AppFixture>
{
    public CheckSuiteHandlerTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    [Fact]
    public async Task Check_Suite_Is_Rerequested_For_Pull_Request_Run_Failure_Associated_With_Workflow_Run()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "1");

        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var checkSuite = CreateCheckSuite(repository);
        var workflowRun = repository.CreateWorkflowRun();

        var checkRun = CreateCheckRun("ubuntu-latest", "completed", "failure");
        checkRun.PullRequests.Add(pullRequest);

        var rerequestCheckSuite = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCheckRuns(repository, checkSuite.Id, checkRun);
        RegisterGetPullRequest(pullRequest);
        RegisterGetWorkflows(checkSuite);

        RegisterRerequestCheckSuite(
            checkSuite,
            (p) => p.WithInterceptionCallback((_) => rerequestCheckSuite.SetResult()));

        var value = CreateWebhook(checkSuite);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await rerequestCheckSuite.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("rando-calrissian", "MEMBER")]
    [InlineData("martincostello", "OWNER")]
    public async Task Check_Suite_Is_Rerequested_For_Pull_Request_Run_Failure_But_Does_Not_Throw_If_Failed(
        string login,
        string authorAssociation)
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "1");

        var owner = CreateUser(login);
        var repository = owner.CreateRepository();

        var pullRequest = repository.CreatePullRequest(owner);
        pullRequest.AuthorAssociation = authorAssociation;

        var checkSuite = CreateCheckSuite(repository);
        checkSuite.PullRequests.Add(pullRequest);

        var workflowRun = repository.CreateWorkflowRun();

        var checkRun = CreateCheckRun("ubuntu-latest", "completed", "failure");
        checkRun.PullRequests.Add(pullRequest);

        var rerequestCheckSuite = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCheckRuns(repository, checkSuite.Id, checkRun);
        RegisterGetPullRequest(pullRequest);
        RegisterGetWorkflows(checkSuite);

        RegisterRerequestCheckSuite(
            checkSuite,
            (p) =>
            {
                p.WithStatus(HttpStatusCode.BadRequest)
                 .WithInterceptionCallback((_) => rerequestCheckSuite.SetResult());
            });

        var value = CreateWebhook(checkSuite);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await rerequestCheckSuite.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(1, "2")]
    [InlineData(2, "2")]
    public async Task Failed_Jobs_Are_Rerun_For_Pull_Request_Run_Failure_Associated_With_Workflow_Run(
        int actualAttempts,
        string maximumAttempts)
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", maximumAttempts);

        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var checkSuite = CreateCheckSuite(repository);
        var workflowRun = repository.CreateWorkflowRun();

        var checkRuns = new List<CheckRunBuilder>();

        for (int i = 0; i < actualAttempts; i++)
        {
            var checkRun = CreateCheckRun("ubuntu-latest", "completed", "failure");
            checkRun.PullRequests.Add(pullRequest);
            checkRuns.Add(checkRun);
        }

        var failedJobsRetried = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCheckRuns(repository, checkSuite.Id, checkRuns.ToArray());
        RegisterGetPullRequest(pullRequest);
        RegisterGetWorkflows(checkSuite, workflowRun);

        RegisterRerunFailedJobs(
            workflowRun,
            (p) => p.WithInterceptionCallback((_) => failedJobsRetried.SetResult()));

        var value = CreateWebhook(checkSuite);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await failedJobsRetried.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Failed_Jobs_Are_Rerun_For_Pull_Request_Run_Failures_Associated_With_Some_Workflow_Runs()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "2");

        var user = CreateUser("dependabot[bot]");
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest(user);

        var checkSuite = CreateCheckSuite(repository);
        checkSuite.PullRequests.Add(pullRequest);

        var workflowRun = repository.CreateWorkflowRun();

        var checkRuns = new List<CheckRunBuilder>();

        for (int i = 0; i < 2; i++)
        {
            var failedRun = CreateCheckRun("ubuntu-latest", "completed", "failure");
            failedRun.PullRequests.Add(pullRequest);

            var successRun = CreateCheckRun("windows-latest", "completed", "success");
            successRun.PullRequests.Add(pullRequest);

            checkRuns.Add(failedRun);
            checkRuns.Add(successRun);
        }

        var failedJobsRetried = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCheckRuns(repository, checkSuite.Id, checkRuns.ToArray());
        RegisterGetPullRequest(pullRequest);
        RegisterGetWorkflows(checkSuite, workflowRun);

        RegisterRerunFailedJobs(
            workflowRun,
            (p) => p.WithInterceptionCallback((_) => failedJobsRetried.SetResult()));

        var value = CreateWebhook(checkSuite);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await failedJobsRetried.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(2, "1")]
    [InlineData(3, "2")]
    public async Task Failed_Jobs_Are_Not_Rerun_For_Pull_Request_Run_Failure_Associated_With_Workflow_Run_If_Too_Many_Attempts(
        int actualAttempts,
        string maximumAttempts)
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", maximumAttempts);

        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var checkSuite = CreateCheckSuite(repository);
        var workflowRun = repository.CreateWorkflowRun();

        var checkRuns = new List<CheckRunBuilder>();

        for (int i = 0; i < actualAttempts; i++)
        {
            var checkRun = CreateCheckRun("windows-latest", "completed", "failure");
            checkRun.PullRequests.Add(pullRequest);
            checkRuns.Add(checkRun);
        }

        var failedJobsRetried = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCheckRuns(repository, checkSuite.Id, checkRuns.ToArray());
        RegisterGetPullRequest(pullRequest);
        RegisterGetWorkflows(checkSuite, workflowRun);

        RegisterRerunFailedJobs(
            workflowRun,
            (p) => p.WithInterceptionCallback((_) => failedJobsRetried.SetResult()));

        var value = CreateWebhook(checkSuite);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(failedJobsRetried);
    }

    [Theory]
    [InlineData("actionRequired")]
    [InlineData("cancelled")]
    [InlineData("neutral")]
    [InlineData("stale")]
    [InlineData("success")]
    [InlineData("timed_out")]
    public async Task Failed_Jobs_Are_Not_Rerun_For_Check_Suite_That_Did_Not_Fail(string conclusion)
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "1");

        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var checkSuite = CreateCheckSuite(repository, conclusion);

        var value = CreateWebhook(checkSuite);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(TimeSpan.FromSeconds(0.5));
    }

    [Fact]
    public async Task Failed_Jobs_Are_Not_Rerun_If_No_Retries_Are_Configured()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "0");

        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var checkSuite = CreateCheckSuite(repository);

        var value = CreateWebhook(checkSuite);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(TimeSpan.FromSeconds(0.5));
    }

    [Fact]
    public async Task Failed_Jobs_Are_Not_Rerun_If_Check_Suite_Cannot_Be_Rerun()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "1");

        var owner = CreateUser();
        var repository = owner.CreateRepository();

        var checkSuite = CreateCheckSuite(repository);
        checkSuite.Rerequestable = false;

        var value = CreateWebhook(checkSuite);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(TimeSpan.FromSeconds(0.5));
    }

    [Fact]
    public async Task Failed_Jobs_Are_Not_Rerun_If_No_Checks_Found()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "1");

        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var checkSuite = CreateCheckSuite(repository);
        var value = CreateWebhook(checkSuite);

        RegisterGetAccessToken();
        RegisterGetPullRequest(pullRequest);
        RegisterGetCheckRuns(repository, checkSuite.Id);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(TimeSpan.FromSeconds(0.5));
    }

    [Fact]
    public async Task Failed_Jobs_Are_Not_Rerun_If_No_Failed_Checks_Found()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "1");

        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var checkSuite = CreateCheckSuite(repository);

        var checkRun = CreateCheckRun("ubuntu-latest", "completed", "success");
        checkRun.PullRequests.Add(pullRequest);

        var value = CreateWebhook(checkSuite);

        RegisterGetAccessToken();
        RegisterGetPullRequest(pullRequest);
        RegisterGetCheckRuns(repository, checkSuite.Id, checkRun);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(TimeSpan.FromSeconds(0.5));
    }

    [Fact]
    public async Task Failed_Jobs_Are_Not_Rerun_If_No_Eligible_Failed_Checks_Found()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "1");

        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var checkSuite = CreateCheckSuite(repository);

        var checkRun = CreateCheckRun("foo", "completed", "failure");
        checkRun.PullRequests.Add(pullRequest);

        RegisterGetAccessToken();
        RegisterGetPullRequest(pullRequest);
        RegisterGetCheckRuns(repository, checkSuite.Id, checkRun);

        var value = CreateWebhook(checkSuite);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(TimeSpan.FromSeconds(0.5));
    }

    [Fact]
    public async Task Check_Suite_Is_Rerequested_But_Does_Not_Throw_If_Failed()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "1");

        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var checkSuite = CreateCheckSuite(repository);
        var workflowRun = repository.CreateWorkflowRun();

        var checkRun = CreateCheckRun("macos-latest", "completed", "failure");
        checkRun.PullRequests.Add(pullRequest);

        var failedJobsRetried = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCheckRuns(repository, checkSuite.Id, checkRun);
        RegisterGetPullRequest(pullRequest);
        RegisterGetWorkflows(checkSuite, workflowRun);

        RegisterRerunFailedJobs(
            workflowRun,
            (p) =>
            {
                p.WithStatus(HttpStatusCode.BadRequest)
                 .WithInterceptionCallback((_) => failedJobsRetried.SetResult());
            });

        var value = CreateWebhook(checkSuite);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await failedJobsRetried.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("rando-calrissian", "COLLABORATOR")]
    [InlineData("rando-calrissian", "CONTRIBUTOR")]
    [InlineData("rando-calrissian", "FIRST_TIMER")]
    [InlineData("rando-calrissian", "FIRST_TIME_CONTRIBUTOR")]
    [InlineData("rando-calrissian", "OWNER")]
    public async Task Failed_Jobs_Are_Not_Rerun_If_Pull_Request_Is_Not_From_A_Trusted_User(
        string login,
        string authorAssociation)
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "1");

        var user = CreateUser(login);
        var owner = CreateUser();
        var repository = owner.CreateRepository();

        var pullRequest = repository.CreatePullRequest(user);
        pullRequest.AuthorAssociation = authorAssociation;

        var checkSuite = CreateCheckSuite(repository);
        checkSuite.PullRequests.Add(pullRequest);

        var value = CreateWebhook(checkSuite);

        RegisterGetAccessToken();
        RegisterGetPullRequest(pullRequest);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(TimeSpan.FromSeconds(0.5));
    }

    [Theory]
    [InlineData("requested")]
    [InlineData("rerequested")]
    public async Task Check_Suite_Is_Ignored_For_Ignored_Action(string action)
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "1");

        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var checkSuite = CreateCheckSuite(repository);

        RegisterGetAccessToken();

        var value = CreateWebhook(checkSuite, action);

        // Act
        using var response = await PostWebhookAsync("check_suite", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Handler_Ignores_Events_That_Are_Not_Check_Suites()
    {
        // Arrange
        var target = Fixture.Services.GetRequiredService<CheckSuiteHandler>();
        var message = new Octokit.Webhooks.Events.IssueComment.IssueCommentCreatedEvent();

        // Act
        await Should.NotThrowAsync(() => target.HandleAsync(message));
    }

    private static CheckSuiteBuilder CreateCheckSuite(
        RepositoryBuilder repository,
        string? conclusion = null,
        bool rerequestable = true)
    {
        return new(repository, "completed", conclusion ?? "failure")
        {
            Rerequestable = rerequestable,
        };
    }

    private static object CreateWebhook(CheckSuiteBuilder checkSuite, string action = "completed")
    {
        return new
        {
            action,
            check_suite = checkSuite.Build(),
            repository = checkSuite.Repository.Build(),
            installation = new
            {
                id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
            },
        };
    }
}
