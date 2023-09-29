// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Drivers;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Handlers;

[Collection(AppCollection.Name)]
public class CheckSuiteHandlerTests(AppFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<AppFixture>(fixture, outputHelper)
{
    [Fact]
    public async Task Check_Suite_Is_Rerequested_For_Pull_Request_Run_Failure_Associated_With_Workflow_Run()
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(1);

        var driver = new CheckSuiteDriver()
            .WithCheckRun(
                (p) => CreateCheckRun(p, "ubuntu-latest", "completed", "failure"));

        RegisterCheckSuiteWithNoWorkflowRun(driver);

        var rerequestCheckSuite = RegisterRerequestCheckSuite(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

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
        Fixture.FailedCheckRerunAttempts(1);

        var driver = new CheckSuiteDriver(login)
            .WithCheckRun(
                (p) => CreateCheckRun(p, "ubuntu-latest", "completed", "failure"));

        driver.PullRequest.AuthorAssociation = authorAssociation;
        driver.CheckSuite.PullRequests.Add(driver.PullRequest);

        RegisterCheckSuiteWithNoWorkflowRun(driver);

        var rerequestCheckSuite = RegisterRerequestCheckSuite(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await rerequestCheckSuite.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    public async Task Failed_Jobs_Are_Rerun_For_Pull_Request_Run_Failure_Associated_With_Workflow_Run(
        int actualAttempts,
        int maximumAttempts)
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(maximumAttempts);

        var driver = new CheckSuiteDriver();

        for (int i = 0; i < actualAttempts; i++)
        {
            driver.WithCheckRun(
                (p) => CreateCheckRun(p, "ubuntu-latest", "completed", "failure"));
        }

        RegisterCheckSuiteWithWorkflowRun(driver);

        var failedJobsRetried = RegisterRerunFailedJobs(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await failedJobsRetried.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Failed_Jobs_Are_Rerun_For_Pull_Request_Run_Failures_Associated_With_Some_Workflow_Runs()
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(2);

        var driver = new CheckSuiteDriver(DependabotCommitter);
        driver.CheckSuite.PullRequests.Add(driver.PullRequest);

        var checkRuns = new List<CheckRunBuilder>();

        for (int i = 0; i < 2; i++)
        {
            driver.WithCheckRun((p) => CreateCheckRun(p, "ubuntu-latest", "completed", "failure"));
            driver.WithCheckRun((p) => CreateCheckRun(p, "windows-latest", "completed", "success"));
        }

        RegisterCheckSuiteWithWorkflowRun(driver);

        var failedJobsRetried = RegisterRerunFailedJobs(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await failedJobsRetried.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Failed_Jobs_Are_Not_Rerun_For_Pull_Request_Run_Failures_Associated_With_Ineligible_Workflow_Run()
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(2);

        var driver = new CheckSuiteDriver(DependabotCommitter);
        driver.CheckSuite.PullRequests.Add(driver.PullRequest);

        var checkRuns = new List<CheckRunBuilder>();

        driver.WithCheckRun((p) => CreateCheckRun(p, "ubuntu-latest", "completed", "failure"));
        driver.WithCheckRun((p) => CreateCheckRun(p, "ubuntu-latest", "completed", "success"));
        driver.WithCheckRun((p) => CreateCheckRun(p, "publish", "completed", "failure"));

        RegisterCheckSuiteWithWorkflowRun(driver);

        var failedJobsRetried = RegisterRerunFailedJobs(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(failedJobsRetried);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    public async Task Failed_Jobs_Are_Not_Rerun_For_Pull_Request_Run_Failure_Associated_With_Workflow_Run_If_Too_Many_Attempts(
        int actualAttempts,
        int maximumAttempts)
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(maximumAttempts);

        var driver = new CheckSuiteDriver();

        var checkRuns = new List<CheckRunBuilder>();

        for (int i = 0; i < actualAttempts; i++)
        {
            driver.WithCheckRun((p) => CreateCheckRun(p, "windows-latest", "completed", "failure"));
        }

        RegisterCheckSuiteWithNoWorkflowRun(driver);

        var failedJobsRetried = RegisterRerunFailedJobs(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

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
        Fixture.FailedCheckRerunAttempts(1);

        var driver = new CheckSuiteDriver(conclusion: conclusion);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await WaitForProcessingAsync();
    }

    [Fact]
    public async Task Failed_Jobs_Are_Not_Rerun_If_No_Retries_Are_Configured()
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(0);

        var driver = new CheckSuiteDriver();

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await WaitForProcessingAsync();
    }

    [Fact]
    public async Task Failed_Jobs_Are_Not_Rerun_If_Check_Suite_Cannot_Be_Rerun()
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(1);

        var driver = new CheckSuiteDriver();

        driver.CheckSuite.Rerequestable = false;

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await WaitForProcessingAsync();
    }

    [Fact]
    public async Task Failed_Jobs_Are_Not_Rerun_If_No_Checks_Found()
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(1);

        var driver = new CheckSuiteDriver();

        RegisterGetAccessToken();
        RegisterGetPullRequest(driver);
        RegisterGetCheckRuns(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await WaitForProcessingAsync();
    }

    [Fact]
    public async Task Failed_Jobs_Are_Not_Rerun_If_No_Failed_Checks_Found()
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(1);

        var driver = new CheckSuiteDriver()
            .WithCheckRun(
                (p) => CreateCheckRun(p, "ubuntu-latest", "completed", "success"));

        RegisterGetAccessToken();
        RegisterGetPullRequest(driver);
        RegisterGetCheckRuns(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await WaitForProcessingAsync();
    }

    [Fact]
    public async Task Failed_Jobs_Are_Not_Rerun_If_No_Eligible_Failed_Checks_Found()
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(1);

        var driver = new CheckSuiteDriver()
            .WithCheckRun(
                (p) => CreateCheckRun(p, "foo", "completed", "failure"));

        RegisterGetAccessToken();
        RegisterGetPullRequest(driver);
        RegisterGetCheckRuns(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await WaitForProcessingAsync();
    }

    [Fact]
    public async Task Check_Suite_Is_Rerequested_But_Does_Not_Throw_If_Failed()
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(1);

        var driver = new CheckSuiteDriver()
            .WithCheckRun(
                (p) => CreateCheckRun(p, "macos-latest", "completed", "failure"));

        RegisterCheckSuiteWithNoWorkflowRun(driver);

        var rerequestCheckSuite = new TaskCompletionSource();
        RegisterRerequestCheckSuite(driver, (p) =>
        {
            p.WithStatus(HttpStatusCode.BadRequest)
             .WithInterceptionCallback((_) => rerequestCheckSuite.SetResult());
        });

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await rerequestCheckSuite.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Failed_Jobs_Are_Rerequested_But_Does_Not_Throw_If_Failed()
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(1);

        var driver = new CheckSuiteDriver()
            .WithCheckRun(
                (p) => CreateCheckRun(p, "macos-latest", "completed", "failure"));

        RegisterCheckSuiteWithWorkflowRun(driver);

        var failedJobsRetried = new TaskCompletionSource();
        RegisterRerunFailedJobs(driver, (p) =>
        {
            p.WithStatus(HttpStatusCode.BadRequest)
             .WithInterceptionCallback((_) => failedJobsRetried.SetResult());
        });

        // Act
        using var response = await PostWebhookAsync(driver);

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
        Fixture.FailedCheckRerunAttempts(1);

        var driver = new CheckSuiteDriver(login);

        driver.CheckSuite.PullRequests.Add(driver.PullRequest);
        driver.PullRequest.AuthorAssociation = authorAssociation;

        RegisterGetAccessToken();
        RegisterGetPullRequest(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await WaitForProcessingAsync();
    }

    [Theory]
    [InlineData("requested")]
    [InlineData("rerequested")]
    public async Task Check_Suite_Is_Ignored_For_Ignored_Action(string action)
    {
        // Arrange
        Fixture.FailedCheckRerunAttempts(1);

        var driver = new CheckSuiteDriver();

        RegisterGetAccessToken();

        // Act
        using var response = await PostWebhookAsync(driver, action);

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

    private static async Task WaitForProcessingAsync()
        => await Task.Delay(TimeSpan.FromSeconds(0.5));

    private async Task<HttpResponseMessage> PostWebhookAsync(CheckSuiteDriver driver, string action = "completed")
    {
        var value = driver.CreateWebhook(action);
        return await PostWebhookAsync("check_suite", value);
    }

    private void RegisterCheckSuiteWithNoWorkflowRun(CheckSuiteDriver driver)
    {
        RegisterGetAccessToken();
        RegisterGetCheckRuns(driver);
        RegisterGetPullRequest(driver);
        RegisterGetWorkflows(driver.CheckSuite);
    }

    private void RegisterCheckSuiteWithWorkflowRun(CheckSuiteDriver driver)
    {
        RegisterGetAccessToken();
        RegisterGetCheckRuns(driver);
        RegisterGetPullRequest(driver);
        RegisterGetWorkflows(driver.CheckSuite, driver.WorkflowRun);
    }

    private void RegisterGetCheckRuns(CheckSuiteDriver driver)
    {
        string path = $"/repos/{driver.Repository.Owner.Login}/{driver.Repository.Name}/check-suites/{driver.CheckSuite.Id}/check-runs";

        var allCheckRuns = driver.CheckRuns.ToArray();

        CreateDefaultBuilder()
            .Requests()
            .ForPath(path)
            .ForQuery("status=completed&filter=all")
            .Responds()
            .WithJsonContent(CreateCheckRuns([.. driver.CheckRuns]))
            .RegisterWith(Fixture.Interceptor);

        var latestCheckRuns = allCheckRuns
            .GroupBy((p) => p.Name)
            .Select((p) => p.Last())
            .ToArray();

        CreateDefaultBuilder()
            .Requests()
            .ForPath(path)
            .ForQuery(string.Empty)
            .Responds()
            .WithJsonContent(CreateCheckRuns(latestCheckRuns))
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetPullRequest(CheckSuiteDriver driver)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{driver.Repository.Owner.Login}/{driver.Repository.Name}/pulls/{driver.PullRequest.Number}")
            .Responds()
            .WithJsonContent(driver.PullRequest)
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetWorkflows(
        CheckSuiteBuilder checkSuite,
        params WorkflowRunBuilder[] workflows)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{checkSuite.Repository.Owner.Login}/{checkSuite.Repository.Name}/actions/runs")
            .ForQuery($"check_suite_id={checkSuite.Id}")
            .Responds()
            .WithJsonContent(CreateWorkflowRuns(workflows))
            .RegisterWith(Fixture.Interceptor);
    }

    private TaskCompletionSource RegisterRerequestCheckSuite(CheckSuiteDriver driver)
    {
        var rerequestCheckSuite = new TaskCompletionSource();

        RegisterRerequestCheckSuite(
            driver,
            (p) => p.WithInterceptionCallback((_) => rerequestCheckSuite.SetResult()));

        return rerequestCheckSuite;
    }

    private void RegisterRerequestCheckSuite(
        CheckSuiteDriver driver,
        Action<HttpRequestInterceptionBuilder> configure)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{driver.Repository.Owner.Login}/{driver.Repository.Name}/check-suites/{driver.CheckSuite.Id}/rerequest")
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(new { });

        configure(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private TaskCompletionSource RegisterRerunFailedJobs(CheckSuiteDriver driver)
    {
        var failedJobsRetried = new TaskCompletionSource();

        RegisterRerunFailedJobs(
            driver,
            (p) => p.WithInterceptionCallback((_) => failedJobsRetried.SetResult()));

        return failedJobsRetried;
    }

    private void RegisterRerunFailedJobs(
        CheckSuiteDriver driver,
        Action<HttpRequestInterceptionBuilder> configure)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{driver.WorkflowRun.Repository.Owner.Login}/{driver.WorkflowRun.Repository.Name}/actions/runs/{driver.WorkflowRun.Id}/rerun-failed-jobs")
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(new { });

        configure(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }
}
