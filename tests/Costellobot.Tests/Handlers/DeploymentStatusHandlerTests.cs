// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Drivers;
using MartinCostello.Costellobot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Handlers;

[Collection(AppCollection.Name)]
public sealed class DeploymentStatusHandlerTests : IntegrationTests<AppFixture>
{
    public DeploymentStatusHandlerTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    [Fact]
    public async Task Deployment_Is_Approved_For_Trusted_User_And_Dependency()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver(
            (repo) => repo.CreateCommit(),
            (repo) => CreateTrustedCommit(repo));

        driver.WithPendingDeployment(
            (commit) => CreateDeployment(commit));

        driver.WithActiveDeployment();
        driver.WithInactiveDeployment();

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        RegisterAllDeployments(driver);
        RegisterCommitComparison(driver);
        RegisterPullRequestForCommit(driver.HeadCommit);

        RegisterApprovePendingDeployment(
            driver,
            (p) => p.WithInterceptionCallback((_) => deploymentApproved.SetResult()));

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await deploymentApproved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Deployment_Is_Approved_For_Trusted_User_And_Multiple_Dependencies()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver(
            (repo) => repo.CreateCommit(),
            (repo) => CreateTrustedCommit(repo));

        driver.WithPendingDeployment(
            (commit) => CreateDeployment(commit));

        driver.WithActiveDeployment();
        driver.WithInactiveDeployment();

        for (int i = 0; i < 5; i++)
        {
            var commit = CreateTrustedCommit(driver.Repository);
            driver.WithPendingCommit(commit);
        }

        foreach (var commit in driver.Commits)
        {
            RegisterPullRequestForCommit(commit);
        }

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        RegisterAllDeployments(driver);
        RegisterCommitComparison(driver);

        RegisterApprovePendingDeployment(
            driver,
            (p) => p.WithInterceptionCallback((_) => deploymentApproved.SetResult()));

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await deploymentApproved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Deployment_Is_Approved_For_Trusted_User_And_Dependency_When_Penultimate_Build_Skipped()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver(
            (repo) => repo.CreateCommit(),
            (repo) => CreateTrustedCommit(repo));

        driver.WithPendingDeployment(
            (commit) => CreateDeployment(commit));

        driver.WithActiveDeployment();
        driver.WithInactiveDeployment();
        driver.WithSkippedDeployment();

        driver.WithPendingCommit(CreateTrustedCommit(driver.Repository));

        foreach (var commit in driver.Commits)
        {
            RegisterPullRequestForCommit(commit);
        }

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        RegisterAllDeployments(driver);
        RegisterCommitComparison(driver);

        RegisterApprovePendingDeployment(
            driver,
            (p) => p.WithInterceptionCallback((_) => deploymentApproved.SetResult()));

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await deploymentApproved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Deployment_Approval_Failure_For_Trusted_User_And_Dependency_Is_Handled()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver(
            (repo) => repo.CreateCommit(),
            (repo) => CreateTrustedCommit(repo));

        driver.WithPendingDeployment(
            (commit) => CreateDeployment(commit));

        driver.WithActiveDeployment();
        driver.WithInactiveDeployment();

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        RegisterAllDeployments(driver);
        RegisterCommitComparison(driver);
        RegisterPullRequestForCommit(driver.HeadCommit);

        RegisterApprovePendingDeployment(driver, (p) =>
        {
            p.WithStatus(HttpStatusCode.Forbidden)
             .WithInterceptionCallback((_) => deploymentApproved.SetResult());
        });

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await deploymentApproved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("in_progress")]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("error")]
    public async Task Deployment_Is_Not_Approved_For_Deployment_That_Is_Not_Waiting(string state)
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver(
            (repo) => repo.CreateCommit(),
            (repo) => CreateTrustedCommit(repo));

        driver.WithPendingDeployment(
            (commit) => CreateDeployment(commit),
            () => CreateDeploymentStatus(state));

        var deploymentApproved = new TaskCompletionSource();

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_Deployment_Approval_Disabled()
    {
        // Arrange
        Fixture.ApproveDeployments(false);

        var driver = new DeploymentStatusDriver();

        driver.WithPendingDeployment(
            (commit) => CreateDeployment(commit));

        var deploymentApproved = new TaskCompletionSource();

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Theory]
    [InlineData("development")]
    [InlineData("staging")]
    public async Task Deployment_Is_Not_Approved_If_Deployment_Environment_Is_Not_Enabled(
        string environment)
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver();

        driver.WithPendingDeployment(
            (_) => CreateDeployment(environment));

        var deploymentApproved = new TaskCompletionSource();

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_No_Deployments_Found()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver();
        driver.WithPendingDeployment();

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterGetDeployments(driver.Repository, driver.PendingDeployment.Environment);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_No_Other_Deployments_Found()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver();
        driver.WithPendingDeployment();

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterAllDeployments(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_No_Deployment_Statuses_Found()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver();

        driver.WithPendingDeployment();
        driver.WithActiveDeployment();

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterAllDeployments(driver);
        RegisterCommitComparison(driver);
        RegisterGetDeploymentStatuses(driver.Repository, driver.ActiveDeployment);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_No_Active_Deployment_Statuses_Found()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver();

        driver.WithPendingDeployment();
        driver.WithActiveDeployment();

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterAllDeployments(driver);
        RegisterCommitComparison(driver);
        RegisterGetDeploymentStatuses(driver.Repository, driver.ActiveDeployment, CreateDeploymentStatus("failure"));

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_No_Active_Deployment_Found()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver();

        driver.WithPendingDeployment();
        driver.WithInactiveDeployment();
        driver.WithInactiveDeployment();

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();
        RegisterAllDeployments(driver);
        RegisterGetDeploymentStatuses(driver.Repository, driver.InactiveDeployments[0], CreateDeploymentStatus("error"));
        RegisterGetDeploymentStatuses(driver.Repository, driver.InactiveDeployments[1]);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_For_No_Diff()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver(
            (repo) => CreateTrustedCommit(repo),
            (repo) => CreateTrustedCommit(repo));

        driver.WithPendingDeployment(
            (commit) => CreateDeployment(commit));

        driver.WithActiveDeployment();
        driver.WithInactiveDeployment();

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        RegisterAllDeployments(driver);
        RegisterCommitComparison(driver.BaseCommit, driver.HeadCommit, new());

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_For_Commits_Behind()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver(
            (repo) => CreateTrustedCommit(repo),
            (repo) => CreateTrustedCommit(repo));

        driver.WithPendingDeployment(
            (commit) => CreateDeployment(commit));

        driver.WithActiveDeployment();
        driver.WithInactiveDeployment();

        var comparison = CreateComparison(driver.HeadCommit);
        comparison.AheadBy = 0;
        comparison.BehindBy = 1;
        comparison.Status = "behind";

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        RegisterAllDeployments(driver);
        RegisterCommitComparison(driver.BaseCommit, driver.HeadCommit, comparison);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_For_Untrusted_User()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver(
            (repo) => repo.CreateCommit(),
            (repo) =>
            {
                var commit = CreateTrustedCommit(repo);
                commit.Author = CreateUser();
                return commit;
            });

        driver.WithPendingDeployment(
            (commit) => CreateDeployment(commit));

        driver.WithActiveDeployment();
        driver.WithInactiveDeployment();

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        RegisterAllDeployments(driver);
        RegisterCommitComparison(driver);
        RegisterPullRequestForCommit(driver.HeadCommit);

        RegisterApprovePendingDeployment(
            driver,
            (p) => p.WithInterceptionCallback((_) => deploymentApproved.SetResult()));

        RegisterGetAccessToken();

        RegisterAllDeployments(driver);
        RegisterCommitComparison(driver);

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_For_Trusted_User_And_Untrusted_Dependency()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver(
            (repo) => CreateTrustedCommit(repo),
            (repo) => CreateTrustedCommit(repo));

        driver.WithPendingDeployment(
            (commit) => CreateDeployment(commit));

        driver.WithActiveDeployment();
        driver.WithInactiveDeployment();

        var dependabot = CreateUserForDependabot();
        var additional = CreateUntrustedCommit(driver.Repository);
        driver.WithPendingCommit(additional);

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        RegisterAllDeployments(driver);
        RegisterCommitComparison(driver);

        foreach (var commit in driver.Commits)
        {
            RegisterPullRequestForCommit(commit);
        }

        RegisterApprovePendingDeployment(
            driver,
            (p) => p.WithInterceptionCallback((_) => deploymentApproved.SetResult()));

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Deployment_Is_Not_Approved_If_Not_Exactly_One_Pending_Deployment(int count)
    {
        // Arrange
        Fixture.ApproveDeployments();

        var driver = new DeploymentStatusDriver(
            (repo) => repo.CreateCommit(),
            (repo) => CreateTrustedCommit(repo));

        driver.WithPendingDeployment(
            (commit) => CreateDeployment(commit));

        driver.WithActiveDeployment();
        driver.WithInactiveDeployment();

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        RegisterAllDeployments(driver);
        RegisterCommitComparison(driver);
        RegisterPullRequestForCommit(driver.HeadCommit);

        RegisterApprovePendingDeployment(
            driver,
            (p) => p.WithInterceptionCallback((_) => deploymentApproved.SetResult()));

        var pendingDeployments = new List<PendingDeploymentBuilder>(count);

        for (int i = 0; i < count; i++)
        {
            pendingDeployments.Add(driver.PendingDeployment.CreatePendingDeployment());
        }

        RegisterGetPendingDeployments(driver.Repository, driver.WorkflowRun.Id, pendingDeployments.ToArray());

        // Act
        using var response = await PostWebhookAsync(driver);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Handler_Ignores_Events_That_Are_Not_Deployment_Statuses()
    {
        // Arrange
        var target = Fixture.Services.GetRequiredService<DeploymentStatusHandler>();
        var message = new Octokit.Webhooks.Events.IssueComment.IssueCommentCreatedEvent();

        // Act
        await Should.NotThrowAsync(() => target.HandleAsync(message));
    }

    private static PullRequestBuilder CreatePullRequestForCommit(GitHubCommitBuilder commit)
    {
        var pullRequest = commit.Repository.CreatePullRequest(commit.Author);

        pullRequest.RefHead = "dependabot/nuget/NodaTimeVersion-3.0.10";

        return pullRequest;
    }

    private static GitHubCommitBuilder CreateTrustedCommit(RepositoryBuilder repo)
    {
        var dependabot = CreateUserForDependabot();

        var commit = repo.CreateCommit(dependabot);
        commit.Message = TrustedCommitMessage();

        return commit;
    }

    private static GitHubCommitBuilder CreateUntrustedCommit(RepositoryBuilder repo)
    {
        var dependabot = CreateUserForDependabot();

        var commit = repo.CreateCommit(dependabot);
        commit.Message = UntrustedCommitMessage();

        return commit;
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(DeploymentStatusDriver driver, string action = "created")
    {
        var value = driver.CreateWebhook(action);
        return await PostWebhookAsync("deployment_status", value);
    }

    private void RegisterSkippedDeployment(DeploymentStatusDriver driver)
    {
        var inactive = CreateDeploymentStatus("inactive");
        var waiting = CreateDeploymentStatus("waiting");

        RegisterGetDeploymentStatuses(
            driver.Repository,
            driver.SkippedDeployment!,
            inactive,
            waiting);
    }

    private void RegisterActiveDeployment(DeploymentStatusDriver driver)
    {
        var success = CreateDeploymentStatus("success");
        var inProgress = CreateDeploymentStatus("in_progress");
        var waiting = CreateDeploymentStatus("waiting");

        RegisterGetDeploymentStatuses(
            driver.Repository,
            driver.ActiveDeployment!,
            success,
            inProgress,
            waiting);
    }

    private void RegisterInactiveDeployments(DeploymentStatusDriver driver)
    {
        foreach (var deployment in driver.InactiveDeployments)
        {
            var inactive = CreateDeploymentStatus("inactive");
            var success = CreateDeploymentStatus("success");
            var inProgress = CreateDeploymentStatus("in_progress");
            var waiting = CreateDeploymentStatus("waiting");

            RegisterGetDeploymentStatuses(
                driver.Repository,
                deployment,
                inactive,
                success,
                inProgress,
                waiting);
        }
    }

    private void RegisterApprovePendingDeployment(
        DeploymentStatusDriver driver,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{driver.Repository.Owner.Login}/{driver.Repository.Name}/actions/runs/{driver.WorkflowRun.Id}/pending_deployments")
            .Responds()
            .WithJsonContent(driver.PendingDeployment!);

        configure?.Invoke(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterCommitComparison(
        GitHubCommitBuilder @base,
        GitHubCommitBuilder head,
        CompareResultBuilder comparison)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{@base.Repository.Owner.Login}/{@base.Repository.Name}/compare/{@base.Sha}...{head.Sha}")
            .Responds()
            .WithJsonContent(comparison)
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterCommitComparison(DeploymentStatusDriver driver)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{driver.BaseCommit.Repository.Owner.Login}/{driver.BaseCommit.Repository.Name}/compare/{driver.BaseCommit.Sha}...{driver.HeadCommit.Sha}")
            .Responds()
            .WithJsonContent(driver.Comparison())
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetDeployments(
        RepositoryBuilder repository,
        string environmentName,
        params DeploymentBuilder[] deployments)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}/deployments")
            .ForQuery($"environment={environmentName}")
            .Responds()
            .WithJsonContent(deployments)
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetDeploymentStatuses(
        RepositoryBuilder repository,
        DeploymentBuilder deployment,
        params DeploymentStatusBuilder[] deploymentStatuses)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}/deployments/{deployment.Id}/statuses")
            .Responds()
            .WithJsonContent(deploymentStatuses)
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterAllDeployments(DeploymentStatusDriver driver)
    {
        var deployments = new List<DeploymentBuilder>();

        if (driver.PendingDeployment is { } pending)
        {
            deployments.Add(pending);
            RegisterPendingDeployment(driver);
        }

        if (driver.SkippedDeployment is { } skipped)
        {
            deployments.Add(skipped);
            RegisterSkippedDeployment(driver);
        }

        if (driver.ActiveDeployment is { } active)
        {
            deployments.Add(active);
            RegisterActiveDeployment(driver);
        }

        if (driver.InactiveDeployments is { Count: > 0 } inactive)
        {
            deployments.AddRange(inactive);
            RegisterInactiveDeployments(driver);
        }

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{driver.Repository.Owner.Login}/{driver.Repository.Name}/deployments")
            .ForQuery($"environment={driver.PendingDeployment!.Environment}")
            .Responds()
            .WithJsonContent(deployments)
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetPendingDeployments(
        RepositoryBuilder repository,
        long runId,
        params PendingDeploymentBuilder[] deployments)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}/actions/runs/{runId}/pending_deployments")
            .Responds()
            .WithJsonContent(deployments)
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterPendingDeployment(DeploymentStatusDriver driver)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{driver.Repository.Owner.Login}/{driver.Repository.Name}/actions/runs/{driver.WorkflowRun.Id}/pending_deployments")
            .Responds()
            .WithJsonContent(driver.PendingDeployment!.CreatePendingDeployment())
            .RegisterWith(Fixture.Interceptor);
    }

    private void RegisterPullRequestForCommit(GitHubCommitBuilder commit)
    {
        var pullRequest = CreatePullRequestForCommit(commit);

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{commit.Repository.Owner.Login}/{commit.Repository.Name}/commits/{commit.Sha}/pulls")
            .Responds()
            .WithJsonContent(pullRequest)
            .RegisterWith(Fixture.Interceptor);
    }
}
