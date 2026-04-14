// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Builders;

using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Drivers;

public class PullRequestDriver
{
    public PullRequestDriver(string? login = null)
    {
        User = CreateUser(login);
        Sender = User;
        Owner = CreateUser();
        Repository = Owner.CreateRepository();
        PullRequest = Repository.CreatePullRequest(User);
        Commit = PullRequest.CreateCommit();
    }

    public GitHubCommitBuilder Commit { get; set; }

    public LabelBuilder? Label { get; set; }

    public UserBuilder Owner { get; set; }

    public PullRequestBuilder PullRequest { get; set; }

    public RepositoryBuilder Repository { get; set; }

    public UserBuilder Sender { get; set; }

    public UserBuilder User { get; set; }

    public static PullRequestDriver ForDependabot()
        => new(DependabotCommitter);

    public static PullRequestDriver ForRenovate()
        => new(RenovateCommitter);

    public PullRequestDriver WithCommitMessage(string message)
    {
        Commit.Message = message;
        return this;
    }

    public PullRequestDriver WithDiff(string diff)
    {
        PullRequest.Diff = diff;
        return this;
    }

    public virtual object CreateWebhook(string action, long? installationId = null)
    {
        installationId ??= long.Parse(InstallationId, CultureInfo.InvariantCulture);

        return new
        {
            action,
            after = PullRequest.ShaHead,
            assignee = User.Build(),
            before = PullRequest.ShaBase,
            changes = new object(),
            number = PullRequest.Number,
            pull_request = PullRequest.Build(),
            repository = PullRequest.Repository.Build(),
            installation = new
            {
                id = installationId.GetValueOrDefault(),
                node_id = InstallationNodeId,
            },
            label = Label?.Build(),
            sender = Sender.Build(),
        };
    }
}
