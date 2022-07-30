// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Builders;

using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Drivers;

public sealed class CheckSuiteDriver
{
    public CheckSuiteDriver(string? login = null, string? conclusion = null)
    {
        Owner = CreateUser(login);
        Repository = Owner.CreateRepository();
        PullRequest = Repository.CreatePullRequest();
        WorkflowRun = Repository.CreateWorkflowRun();
        CheckSuite = CreateCheckSuite(conclusion);
    }

    public IList<CheckRunBuilder> CheckRuns { get; set; } = new List<CheckRunBuilder>();

    public CheckSuiteBuilder CheckSuite { get; set; }

    public UserBuilder Owner { get; set; }

    public PullRequestBuilder PullRequest { get; set; }

    public RepositoryBuilder Repository { get; set; }

    public WorkflowRunBuilder WorkflowRun { get; set; }

    public CheckSuiteDriver WithCheckRun(Func<PullRequestBuilder, CheckRunBuilder> configure)
    {
        CheckRuns.Add(configure(this.PullRequest));
        return this;
    }

    public object CreateWebhook(string action)
    {
        return new
        {
            action,
            check_suite = CheckSuite.Build(),
            repository = CheckSuite.Repository.Build(),
            installation = new
            {
                id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
            },
        };
    }

    private CheckSuiteBuilder CreateCheckSuite(
        string? conclusion = null,
        bool rerequestable = true)
    {
        return new(Repository, "completed", conclusion ?? "failure")
        {
            Rerequestable = rerequestable,
        };
    }
}
