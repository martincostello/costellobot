// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Moq;

namespace MartinCostello.Costellobot;

public static class ObjectExtensions
{
    public static IOptionsMonitor<T> ToMonitor<T>(this T options)
        where T : class
    {
        var mock = new Mock<IOptionsMonitor<T>>();

        mock.Setup((p) => p.CurrentValue)
            .Returns(options);

        return mock.Object;
    }

    public static IOptionsSnapshot<T> ToSnapshot<T>(this T options)
        where T : class
    {
        var mock = new Mock<IOptionsSnapshot<T>>();

        mock.Setup((p) => p.Value)
            .Returns(options);

        return mock.Object;
    }
}
