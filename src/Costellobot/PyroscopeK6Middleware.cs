﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

internal sealed class PyroscopeK6Middleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    [System.Diagnostics.StackTraceHidden]
    public Task InvokeAsync(HttpContext context) =>
        ApplicationTelemetry.ExecuteWithProfilerAsync(
            (_next, context),
            static (state) => state._next(state.context));
}
