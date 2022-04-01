// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public static class ILoggingBuilderExtensions
{
    public static ILoggingBuilder AddSignalR(this ILoggingBuilder builder)
    {
        builder.Services.AddSingleton<ILoggerProvider, ClientLoggingProvider>();
        return builder;
    }
}
