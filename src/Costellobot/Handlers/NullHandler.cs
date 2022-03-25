// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Terrajobst.GitHubEvents;

namespace MartinCostello.Costellobot.Handlers;

public sealed class NullHandler : IHandler
{
    public static readonly NullHandler Instance = new();

    public Task HandleAsync(GitHubEvent message) => Task.CompletedTask;
}
