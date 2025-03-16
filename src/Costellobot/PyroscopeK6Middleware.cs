// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Pyroscope;

namespace MartinCostello.Costellobot;

internal sealed class PyroscopeK6Middleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (ApplicationTelemetry.ExtractK6Baggage() is { Count: > 0 } baggage)
        {
            try
            {
                Profiler.Instance.ClearDynamicTags();

                foreach ((string key, string value) in baggage)
                {
                    Profiler.Instance.SetDynamicTag(key, value);
                }

                await _next(context).ConfigureAwait(false);
            }
            finally
            {
                Profiler.Instance.ClearDynamicTags();
            }
        }
        else
        {
            await _next(context).ConfigureAwait(false);
        }
    }
}
