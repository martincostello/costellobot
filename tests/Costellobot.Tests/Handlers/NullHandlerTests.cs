// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Handlers;

public static class NullHandlerTests
{
    [Fact]
    public static async Task Null_Handler_Does_Nothing()
    {
        // Arrange
        var target = NullHandler.Instance;

        // Act (no Assert)
        await target.HandleAsync(new Octokit.Webhooks.Events.PingEvent());
    }
}
