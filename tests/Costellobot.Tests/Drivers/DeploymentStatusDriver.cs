// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using MartinCostello.Costellobot.Builders;

using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Drivers;

public sealed class DeploymentStatusDriver
{
    public DeploymentStatusDriver(
        Func<RepositoryBuilder, GitHubCommitBuilder>? baseCommit = null,
        Func<RepositoryBuilder, GitHubCommitBuilder>? headCommit = null)
    {
        Owner = CreateUser();
        Repository = Owner.CreateRepository();
        WorkflowRun = Repository.CreateWorkflowRun();
        BaseCommit = baseCommit?.Invoke(Repository) ?? Repository.CreateCommit();
        HeadCommit = headCommit?.Invoke(Repository) ?? Repository.CreateCommit();
        Commits.Add(HeadCommit);
    }

    public GitHubCommitBuilder BaseCommit { get; set; }

    public GitHubCommitBuilder HeadCommit { get; set; }

    public IList<GitHubCommitBuilder> Commits { get; } = new List<GitHubCommitBuilder>();

    public DeploymentBuilder? ActiveDeployment { get; set; }

    public IList<DeploymentBuilder> InactiveDeployments { get; } = new List<DeploymentBuilder>();

    public DeploymentBuilder? PendingDeployment { get; set; }

    public DeploymentBuilder? SkippedDeployment { get; set; }

    public DeploymentStatusBuilder? PendingDeploymentStatus { get; set; }

    public UserBuilder Owner { get; set; }

    public RepositoryBuilder Repository { get; set; }

    public WorkflowRunBuilder WorkflowRun { get; set; }

    [MemberNotNull(nameof(ActiveDeployment))]
    public DeploymentStatusDriver WithActiveDeployment(string? environmentName = null)
    {
        ActiveDeployment = CreateDeployment(environmentName ?? PendingDeployment!.Environment, BaseCommit.Sha);
        return this;
    }

    public DeploymentStatusDriver WithInactiveDeployment(
        string? environmentName = null,
        Func<RepositoryBuilder, GitHubCommitBuilder>? commitFactory = null)
    {
        var commit = commitFactory?.Invoke(Repository) ?? Repository.CreateCommit();
        var deployment = CreateDeployment(environmentName ?? PendingDeployment!.Environment, commit?.Sha);

        InactiveDeployments.Add(deployment);

        return this;
    }

    [MemberNotNull(nameof(PendingDeployment))]
    [MemberNotNull(nameof(PendingDeploymentStatus))]
    public DeploymentStatusDriver WithPendingDeployment(
        Func<GitHubCommitBuilder, DeploymentBuilder>? deploymentFactory = null,
        Func<DeploymentStatusBuilder>? statusFactory = null)
    {
        PendingDeployment = deploymentFactory?.Invoke(HeadCommit) ?? CreateDeployment();
        PendingDeploymentStatus = statusFactory?.Invoke() ?? CreateDeploymentStatus();

        return this;
    }

    [MemberNotNull(nameof(SkippedDeployment))]
    public DeploymentStatusDriver WithSkippedDeployment(
        string? environmentName = null,
        Func<RepositoryBuilder, GitHubCommitBuilder>? commitFactory = null)
    {
        var commit = commitFactory?.Invoke(Repository);
        SkippedDeployment = CreateDeployment(environmentName ?? PendingDeployment!.Environment, commit?.Sha);
        return this;
    }

    public DeploymentStatusDriver WithPendingCommit(GitHubCommitBuilder commit)
    {
        Commits.Add(commit);
        return this;
    }

    public CompareResultBuilder Comparison()
        => CreateComparison(Commits.ToArray());

    public object CreateWebhook(string action)
    {
        return new
        {
            action,
            deployment_status = PendingDeploymentStatus!.Build(),
            deployment = PendingDeployment!.Build(),
            check_run = new { },
            workflow = new { },
            workflow_run = WorkflowRun.Build(),
            repository = Repository.Build(),
            installation = new
            {
                id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
            },
        };
    }
}
