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
    public async Task Check_Suite_Is_Rerequested_For_Pull_Request_Run_Failure()
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", "1");

        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var checkSuite = CreateCheckSuite(repository);
        var workflowRun = repository.CreateWorkflowRun();

        var checkRun = CreateCheckRun("windows-latest", "completed", "failure");
        checkRun.PullRequests.Add(pullRequest);

        var failedJobsRetried = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetCheckRuns(repository, checkSuite.Id, checkRun);
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

        // Act (no Assert)
        await target.HandleAsync(message);
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

    private static async Task AssertTaskNotRun(TaskCompletionSource source)
    {
        await Task.Delay(TimeSpan.FromSeconds(0.1));
        source.Task.Status.ShouldBe(TaskStatus.WaitingForActivation);
    }
}
