// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Builders;

using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Drivers;

public sealed class RepositoryDispatchDriver
{
    public RepositoryDispatchDriver()
    {
        Owner = CreateUser();
        Repository = Owner.CreateRepository();
    }

    public string Branch { get; set; } = "main";

    public object? ClientPayload { get; set; }

    public UserBuilder Owner { get; set; }

    public RepositoryBuilder Repository { get; set; }

    public object CreateWebhook(string action)
    {
        return new
        {
            action,
            branch = Branch,
            client_payload = ClientPayload,
            repository = Repository.Build(),
            sender = Owner.Build(),
            installation = new
            {
                id = long.Parse(InstallationId, CultureInfo.InvariantCulture),
            },
        };
    }
}
