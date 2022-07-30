// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using MartinCostello.Costellobot.Builders;
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

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var workflowRun = repo.CreateWorkflowRun();

        var newCommit = CreateTrustedCommit(repo);
        var pullRequest = CreatePullRequestForCommit(newCommit);

        var pendingDeployment = CreateDeployment(sha: newCommit.Sha);
        var deploymentStatus = CreateDeploymentStatus();

        var otherUser = CreateUser();
        var existingCommit = repo.CreateCommit(otherUser);

        var activeDeployment = RegisterActiveDeployment(repo, pendingDeployment.Environment);
        activeDeployment.Sha = existingCommit.Sha;

        var previousDeployment = RegisterInactiveDeployment(repo, pendingDeployment.Environment);

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        var comparison = CreateComparison(newCommit);

        RegisterGetCompare(existingCommit, newCommit, comparison);

        RegisterGetDeployments(
            repo,
            pendingDeployment.Environment,
            pendingDeployment,
            activeDeployment,
            previousDeployment);

        RegisterGetPendingDeployments(repo, workflowRun.Id, pendingDeployment.CreatePendingDeployment());

        RegisterGetPullRequestsForCommit(repo, newCommit.Sha, pullRequest);

        RegisterApprovePendingDeployments(
            repo,
            workflowRun.Id,
            pendingDeployment,
            (p) => p.WithInterceptionCallback((_) => deploymentApproved.SetResult()));

        var value = CreateWebhook(repo, pendingDeployment, deploymentStatus, workflowRun);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await deploymentApproved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Deployment_Is_Approved_For_Trusted_User_And_Multiple_Dependencies()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var workflowRun = repo.CreateWorkflowRun();

        var headCommit = CreateTrustedCommit(repo);

        var pendingDeployment = CreateDeployment(sha: headCommit.Sha);
        var deploymentStatus = CreateDeploymentStatus();

        var otherUser = CreateUser();
        var baseCommit = repo.CreateCommit(otherUser);

        var activeDeployment = RegisterActiveDeployment(repo, pendingDeployment.Environment);
        activeDeployment.Sha = baseCommit.Sha;

        var previousDeployment = RegisterInactiveDeployment(repo, pendingDeployment.Environment);

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        var trustedCommits = new[]
        {
            headCommit,
            CreateTrustedCommit(repo),
            CreateTrustedCommit(repo),
            CreateTrustedCommit(repo),
            CreateTrustedCommit(repo),
            CreateTrustedCommit(repo),
        };

        foreach (var commit in trustedCommits)
        {
            var pullRequest = CreatePullRequestForCommit(commit);
            RegisterGetPullRequestsForCommit(repo, commit.Sha, pullRequest);
        }

        var comparison = CreateComparison(trustedCommits);

        RegisterGetCompare(baseCommit, headCommit, comparison);

        RegisterGetDeployments(
            repo,
            pendingDeployment.Environment,
            pendingDeployment,
            activeDeployment,
            previousDeployment);

        RegisterGetPendingDeployments(repo, workflowRun.Id, pendingDeployment.CreatePendingDeployment());

        RegisterApprovePendingDeployments(
            repo,
            workflowRun.Id,
            pendingDeployment,
            (p) => p.WithInterceptionCallback((_) => deploymentApproved.SetResult()));

        var value = CreateWebhook(repo, pendingDeployment, deploymentStatus, workflowRun);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await deploymentApproved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Deployment_Is_Approved_For_Trusted_User_And_Dependency_When_Penultimate_Build_Skipped()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var workflowRun = repo.CreateWorkflowRun();

        var newCommit = CreateTrustedCommit(repo);
        var pullRequest = CreatePullRequestForCommit(newCommit);

        var pendingDeployment = CreateDeployment(sha: newCommit.Sha);
        var deploymentStatus = CreateDeploymentStatus();

        var otherUser = CreateUser();
        var existingCommit = repo.CreateCommit(otherUser);

        var skippedDeployment = RegisterSkippedDeployment(repo, pendingDeployment.Environment);

        var activeDeployment = RegisterActiveDeployment(repo, pendingDeployment.Environment);
        activeDeployment.Sha = existingCommit.Sha;

        var previousDeployment = RegisterInactiveDeployment(repo, pendingDeployment.Environment);

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        var comparison = CreateComparison(newCommit);

        RegisterGetCompare(existingCommit, newCommit, comparison);

        RegisterGetDeployments(
            repo,
            pendingDeployment.Environment,
            pendingDeployment,
            skippedDeployment,
            activeDeployment,
            previousDeployment);

        RegisterGetPendingDeployments(repo, workflowRun.Id, pendingDeployment.CreatePendingDeployment());

        RegisterGetPullRequestsForCommit(repo, newCommit.Sha, pullRequest);

        RegisterApprovePendingDeployments(
            repo,
            workflowRun.Id,
            pendingDeployment,
            (p) => p.WithInterceptionCallback((_) => deploymentApproved.SetResult()));

        var value = CreateWebhook(repo, pendingDeployment, deploymentStatus, workflowRun);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await deploymentApproved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Deployment_Approval_Failure_For_Trusted_User_And_Dependency_Is_Handled()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var workflowRun = repo.CreateWorkflowRun();

        var newCommit = CreateTrustedCommit(repo);
        var pullRequest = CreatePullRequestForCommit(newCommit);

        var pendingDeployment = CreateDeployment(sha: newCommit.Sha);
        var deploymentStatus = CreateDeploymentStatus();

        var otherUser = CreateUser();
        var existingCommit = repo.CreateCommit(otherUser);

        var activeDeployment = RegisterActiveDeployment(repo, pendingDeployment.Environment);
        activeDeployment.Sha = existingCommit.Sha;

        var previousDeployment = RegisterInactiveDeployment(repo, pendingDeployment.Environment);

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        var comparison = CreateComparison(newCommit);

        RegisterGetCompare(existingCommit, newCommit, comparison);

        RegisterGetDeployments(
            repo,
            pendingDeployment.Environment,
            pendingDeployment,
            activeDeployment,
            previousDeployment);

        RegisterGetPendingDeployments(repo, workflowRun.Id, pendingDeployment.CreatePendingDeployment());

        RegisterGetPullRequestsForCommit(repo, newCommit.Sha, pullRequest);

        RegisterApprovePendingDeployments(
            repo,
            workflowRun.Id,
            pendingDeployment,
            (p) =>
            {
                p.WithStatus(HttpStatusCode.Forbidden)
                 .WithInterceptionCallback((_) => deploymentApproved.SetResult());
            });

        var value = CreateWebhook(repo, pendingDeployment, deploymentStatus, workflowRun);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

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

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var deployment = CreateDeployment();
        var deploymentStatus = CreateDeploymentStatus(state);

        var deploymentApproved = new TaskCompletionSource();

        var value = CreateWebhook(repo, deployment, deploymentStatus);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_Deployment_Approval_Disabled()
    {
        // Arrange
        Fixture.ApproveDeployments(false);

        var owner = CreateUser();
        var repo = owner.CreateRepository();

        var deployment = CreateDeployment();
        var deploymentStatus = CreateDeploymentStatus();

        var deploymentApproved = new TaskCompletionSource();

        var value = CreateWebhook(repo, deployment, deploymentStatus);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

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

        var owner = CreateUser();
        var repo = owner.CreateRepository();

        var deployment = CreateDeployment(environment);
        var deploymentStatus = CreateDeploymentStatus();

        var deploymentApproved = new TaskCompletionSource();

        var value = CreateWebhook(repo, deployment, deploymentStatus);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_No_Deployments_Found()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var owner = CreateUser();
        var repo = owner.CreateRepository();

        var deployment = CreateDeployment();
        var deploymentStatus = CreateDeploymentStatus();

        var deploymentApproved = new TaskCompletionSource();

        var value = CreateWebhook(repo, deployment, deploymentStatus);

        RegisterGetAccessToken();
        RegisterGetDeployments(repo, deployment.Environment);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_No_Other_Deployments_Found()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var owner = CreateUser();
        var repo = owner.CreateRepository();

        var deployment = CreateDeployment();
        var deploymentStatus = CreateDeploymentStatus();

        var deploymentApproved = new TaskCompletionSource();

        var value = CreateWebhook(repo, deployment, deploymentStatus);

        RegisterGetAccessToken();
        RegisterGetDeployments(repo, deployment.Environment, deployment);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_No_Deployment_Statuses_Found()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var owner = CreateUser();
        var repo = owner.CreateRepository();

        var pendingDeployment = CreateDeployment();
        var previousDeployment = CreateDeployment();
        var deploymentStatus = CreateDeploymentStatus();

        var deploymentApproved = new TaskCompletionSource();

        var value = CreateWebhook(repo, pendingDeployment, deploymentStatus);

        RegisterGetAccessToken();
        RegisterGetDeployments(repo, pendingDeployment.Environment, pendingDeployment, previousDeployment);
        RegisterGetDeploymentStatuses(repo, previousDeployment);
        RegisterGetDeploymentStatuses(repo, pendingDeployment);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_No_Active_Deployment_Statuses_Found()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var owner = CreateUser();
        var repo = owner.CreateRepository();

        var pendingDeployment = CreateDeployment();
        var previousDeployment = CreateDeployment();
        var deploymentStatus = CreateDeploymentStatus();

        var deploymentApproved = new TaskCompletionSource();

        var value = CreateWebhook(repo, pendingDeployment, deploymentStatus);

        RegisterGetAccessToken();
        RegisterGetDeployments(repo, pendingDeployment.Environment, pendingDeployment, previousDeployment);
        RegisterGetDeploymentStatuses(repo, previousDeployment, CreateDeploymentStatus("failure"));
        RegisterGetDeploymentStatuses(repo, pendingDeployment, deploymentStatus);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_If_No_Active_Deployment_Found()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var owner = CreateUser();
        var repo = owner.CreateRepository();

        var pendingDeployment = CreateDeployment();
        var previousDeployment1 = CreateDeployment();
        var previousDeployment2 = CreateDeployment();
        var deploymentStatus = CreateDeploymentStatus();

        var deploymentApproved = new TaskCompletionSource();

        var value = CreateWebhook(repo, pendingDeployment, deploymentStatus);

        RegisterGetAccessToken();
        RegisterGetDeployments(repo, pendingDeployment.Environment, pendingDeployment, previousDeployment1, previousDeployment2);
        RegisterGetDeploymentStatuses(repo, previousDeployment1, CreateDeploymentStatus("error"));
        RegisterGetDeploymentStatuses(repo, previousDeployment2);
        RegisterGetDeploymentStatuses(repo, pendingDeployment, deploymentStatus);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_For_No_Diff()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var workflowRun = repo.CreateWorkflowRun();

        var commit = CreateTrustedCommit(repo);

        var pendingDeployment = CreateDeployment(sha: commit.Sha);
        var deploymentStatus = CreateDeploymentStatus();

        var activeDeployment = RegisterActiveDeployment(repo, pendingDeployment.Environment);
        activeDeployment.Sha = commit.Sha;

        var previousDeployment = RegisterInactiveDeployment(repo, pendingDeployment.Environment);

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        var comparison = CreateComparison();

        RegisterGetCompare(commit, commit, comparison);

        RegisterGetDeployments(
            repo,
            pendingDeployment.Environment,
            pendingDeployment,
            activeDeployment,
            previousDeployment);

        var value = CreateWebhook(repo, pendingDeployment, deploymentStatus, workflowRun);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_For_Commits_Behind()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var workflowRun = repo.CreateWorkflowRun();

        var commit = CreateTrustedCommit(repo);

        var pendingDeployment = CreateDeployment(sha: commit.Sha);
        var deploymentStatus = CreateDeploymentStatus();

        var activeDeployment = RegisterActiveDeployment(repo, pendingDeployment.Environment);
        activeDeployment.Sha = commit.Sha;

        var previousDeployment = RegisterInactiveDeployment(repo, pendingDeployment.Environment);

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        var comparison = CreateComparison(commit);
        comparison.Status = "behind";
        comparison.AheadBy = 0;
        comparison.BehindBy = 1;

        RegisterGetCompare(commit, commit, comparison);

        RegisterGetDeployments(
            repo,
            pendingDeployment.Environment,
            pendingDeployment,
            activeDeployment,
            previousDeployment);

        var value = CreateWebhook(repo, pendingDeployment, deploymentStatus, workflowRun);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_For_Untrusted_User()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var workflowRun = repo.CreateWorkflowRun();

        var newCommit = CreateTrustedCommit(repo);
        newCommit.Author = CreateUser();

        var pendingDeployment = CreateDeployment(sha: newCommit.Sha);
        var deploymentStatus = CreateDeploymentStatus();

        var otherUser = CreateUser();
        var existingCommit = repo.CreateCommit(otherUser);

        var activeDeployment = RegisterActiveDeployment(repo, pendingDeployment.Environment);
        activeDeployment.Sha = existingCommit.Sha;

        var previousDeployment = RegisterInactiveDeployment(repo, pendingDeployment.Environment);

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        var comparison = CreateComparison(newCommit);

        RegisterGetCompare(existingCommit, newCommit, comparison);

        RegisterGetDeployments(
            repo,
            pendingDeployment.Environment,
            pendingDeployment,
            activeDeployment,
            previousDeployment);

        var value = CreateWebhook(repo, pendingDeployment, deploymentStatus, workflowRun);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertTaskNotRun(deploymentApproved);
    }

    [Fact]
    public async Task Deployment_Is_Not_Approved_For_Trusted_User_And_Untrusted_Dependency()
    {
        // Arrange
        Fixture.ApproveDeployments();

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var workflowRun = repo.CreateWorkflowRun();

        var headCommit = CreateTrustedCommit(repo);
        var additionalCommit = CreateUntrustedCommit(repo);

        var pullRequestForHead = CreatePullRequestForCommit(headCommit);
        var pullRequestForAdditional = CreatePullRequestForCommit(additionalCommit);

        var pendingDeployment = CreateDeployment(sha: headCommit.Sha);
        var deploymentStatus = CreateDeploymentStatus();

        var otherUser = CreateUser();
        var baseCommit = repo.CreateCommit(otherUser);

        var activeDeployment = RegisterActiveDeployment(repo, pendingDeployment.Environment);
        activeDeployment.Sha = baseCommit.Sha;

        var previousDeployment = RegisterInactiveDeployment(repo, pendingDeployment.Environment);

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        var comparison = CreateComparison(headCommit, additionalCommit);

        RegisterGetCompare(baseCommit, headCommit, comparison);

        RegisterGetDeployments(
            repo,
            pendingDeployment.Environment,
            pendingDeployment,
            activeDeployment,
            previousDeployment);

        RegisterGetPullRequestsForCommit(repo, headCommit.Sha, pullRequestForHead);
        RegisterGetPullRequestsForCommit(repo, additionalCommit.Sha, pullRequestForAdditional);

        var value = CreateWebhook(repo, pendingDeployment, deploymentStatus, workflowRun);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

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

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var workflowRun = repo.CreateWorkflowRun();

        var newCommit = CreateTrustedCommit(repo);

        var pendingDeployment = CreateDeployment(sha: newCommit.Sha);
        var deploymentStatus = CreateDeploymentStatus();

        var otherUser = CreateUser();
        var existingCommit = repo.CreateCommit(otherUser);

        var activeDeployment = RegisterActiveDeployment(repo, pendingDeployment.Environment);
        activeDeployment.Sha = existingCommit.Sha;

        var previousDeployment = RegisterInactiveDeployment(repo, pendingDeployment.Environment);

        var deploymentApproved = new TaskCompletionSource();

        RegisterGetAccessToken();

        var comparison = CreateComparison(newCommit);

        RegisterGetCompare(existingCommit, newCommit, comparison);

        RegisterGetDeployments(
            repo,
            pendingDeployment.Environment,
            pendingDeployment,
            activeDeployment,
            previousDeployment);

        var pendingDeployments = new List<PendingDeploymentBuilder>(count);

        for (int i = 0; i < count; i++)
        {
            pendingDeployments.Add(pendingDeployment.CreatePendingDeployment());
        }

        RegisterGetPendingDeployments(repo, workflowRun.Id, pendingDeployments.ToArray());

        var value = CreateWebhook(repo, pendingDeployment, deploymentStatus, workflowRun);

        // Act
        using var response = await PostWebhookAsync("deployment_status", value);

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

    private static object CreateWebhook(
        RepositoryBuilder repository,
        DeploymentBuilder deployment,
        DeploymentStatusBuilder deploymentStatus,
        WorkflowRunBuilder? workflowRun = null,
        string action = "created")
    {
        return new
        {
            action,
            deployment_status = deploymentStatus.Build(),
            deployment = deployment.Build(),
            check_run = new { },
            workflow = new { },
            workflow_run = workflowRun?.Build() ?? new { },
            repository = repository.Build(),
            installation = new
            {
                id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
            },
        };
    }

    private DeploymentBuilder RegisterSkippedDeployment(RepositoryBuilder repo, string environmentName)
    {
        var deployment = CreateDeployment(environmentName);

        var inactive = CreateDeploymentStatus("inactive");
        var waiting = CreateDeploymentStatus("waiting");

        RegisterGetDeploymentStatuses(repo, deployment, inactive, waiting);

        return deployment;
    }

    private DeploymentBuilder RegisterActiveDeployment(RepositoryBuilder repo, string environmentName)
    {
        var deployment = CreateDeployment(environmentName);

        var success = CreateDeploymentStatus("success");
        var inProgress = CreateDeploymentStatus("in_progress");
        var waiting = CreateDeploymentStatus("waiting");

        RegisterGetDeploymentStatuses(repo, deployment, success, inProgress, waiting);

        return deployment;
    }

    private DeploymentBuilder RegisterInactiveDeployment(RepositoryBuilder repo, string environmentName)
    {
        var deployment = CreateDeployment(environmentName);

        var inactive = CreateDeploymentStatus("inactive");
        var success = CreateDeploymentStatus("success");
        var inProgress = CreateDeploymentStatus("in_progress");
        var waiting = CreateDeploymentStatus("waiting");

        RegisterGetDeploymentStatuses(repo, deployment, inactive, success, inProgress, waiting);

        return deployment;
    }
}
