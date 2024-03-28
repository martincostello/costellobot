// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using MartinCostello.Costellobot.Builders;

using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Drivers;

public sealed class DeploymentProtectionRuleDriver
{
    public DeploymentProtectionRuleDriver(DeploymentBuilder deployment)
    {
        Owner = CreateUser();
        Repository = Owner.CreateRepository();
        Deployment = deployment;
        Environment = Deployment.Environment ?? "production";
    }

    public DeploymentBuilder Deployment { get; set; }

    public string Environment { get; set; }

    public string Event { get; set; } = "push";

    public UserBuilder Owner { get; set; }

    public IList<PullRequestBuilder> PullRequests { get; set; } = [];

    public RepositoryBuilder Repository { get; set; }

    public long RunId { get; set; } = RandomNumberGenerator.GetInt32(int.MaxValue);

    public object CreateWebhook(string action)
    {
        return new
        {
            action,
            environment = Environment,
            @event = Event,
            deployment_callback_url = $"https://api.github.com/repos/{Repository.Owner.Login}/{Repository.Name}/actions/runs/{RunId}/deployment_protection_rule",
            deployment = Deployment.Build(),
            pull_requests = PullRequests.Build(),
            repository = Repository.Build(),
            installation = new
            {
                id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
            },
        };
    }
}
