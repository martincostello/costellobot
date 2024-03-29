﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using NSubstitute;

namespace MartinCostello.Costellobot;

public static class ObjectExtensions
{
    public static IOptionsMonitor<T> ToMonitor<T>(this T options)
        where T : class
    {
        var monitor = Substitute.For<IOptionsMonitor<T>>();

        monitor.CurrentValue.Returns(options);

        return monitor;
    }

    public static IOptionsSnapshot<T> ToSnapshot<T>(this T options)
        where T : class
    {
        var snapshot = Substitute.For<IOptionsSnapshot<T>>();

        snapshot.Value.Returns(options);

        return snapshot;
    }
}
