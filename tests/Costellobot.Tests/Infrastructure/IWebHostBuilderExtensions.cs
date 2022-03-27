// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class IWebHostBuilderExtensions
{
    public static IWebHostBuilder ConfigureAntiforgeryTokenResource(this IWebHostBuilder builder)
    {
        return builder.ConfigureServices((services) =>
        {
            services.AddTransient<IStartupFilter, AddMvcStartupFilter>()
                    .AddControllers()
                    .AddApplicationPart(typeof(AntiforgeryTokenController).Assembly)
                    .AddControllersAsServices();
        });
    }

    private sealed class AddMvcStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return (builder) =>
            {
                next(builder);
                builder.UseRouting();
                builder.UseEndpoints((p) => p.MapControllers());
            };
        }
    }
}
