// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Builders;

using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Drivers;

public sealed class PushDriver
{
    public PushDriver(bool isFork = false)
    {
        Owner = Sender = User = CreateUser();
        Repository = Owner.CreateRepository(isFork: isFork);
    }

    public bool Created { get; set; }

    public IList<GitCommitBuilder> Commits { get; set; } = new List<GitCommitBuilder>();

    public bool Deleted { get; set; }

    public bool Forced { get; set; }

    public UserBuilder Owner { get; set; }

    public string Ref { get; set; } = "refs/heads/main";

    public RepositoryBuilder Repository { get; set; }

    public UserBuilder Sender { get; set; }

    public UserBuilder User { get; set; }

    public object CreateWebhook()
    {
        return new
        {
            @ref = Ref,
            repository = Repository.Build(),
            installation = new
            {
                id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
            },
            sender = Sender.Build(),
            created = Created,
            deleted = Deleted,
            forced = Forced,
            commits = Commits.Build(),
        };
    }
}
