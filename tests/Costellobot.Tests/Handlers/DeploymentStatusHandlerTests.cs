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

    [Theory]
    [InlineData("pending")]
    [InlineData("in_progress")]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("error")]
    public async Task Deployment_Is_Not_Approved_For_Deployment_That_Is_Not_Waiting(string state)
    {
        // Arrange
        Fixture.OverrideConfiguration("Webhook:Deploy", bool.TrueString);

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var status = CreateDeploymentStatus(state);

        var deploymentApproved = new TaskCompletionSource();

        var value = CreateWebhook(repo, status);

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
        Fixture.OverrideConfiguration("Webhook:Deploy", bool.FalseString);

        var owner = CreateUser();
        var repo = owner.CreateRepository();
        var status = CreateDeploymentStatus("waiting");

        var deploymentApproved = new TaskCompletionSource();

        var value = CreateWebhook(repo, status);

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

        // Act (no Assert)
        await target.HandleAsync(message);
    }

    private static object CreateWebhook(
        RepositoryBuilder repository,
        DeploymentStatusBuilder deploymentStatus,
        string action = "created")
    {
        return new
        {
            action,
            deployment_status = deploymentStatus.Build(),
            deployment = new
            {
            },
            check_run = new
            {
            },
            workflow = new
            {
            },
            workflow_run = new
            {
            },
            repository = repository.Build(),
            installation = new
            {
                id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
            },
        };
    }
}
