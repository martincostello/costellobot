// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Handlers;

public sealed class HandlerFactory : IHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;

    public HandlerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IHandler Create(string eventType)
    {
        return eventType switch
        {
            "pull_request" => _serviceProvider.GetRequiredService<PullRequestHandler>(),
            _ => NullHandler.Instance,
        };
    }
}
