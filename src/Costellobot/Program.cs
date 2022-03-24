// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable SA1516

using MartinCostello.Costellobot;
using Terrajobst.GitHubEvents.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGitHub(builder.Configuration);

builder.Host.ConfigureApplication();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("https://martincostello.com/"));

app.MapGitHubWebHook(secret: app.Configuration["GitHub:WebhookSecret"]);

app.Run();

namespace MartinCostello.Costellobot
{
    public partial class Program
    {
        // Expose the Program class for use with WebApplicationFactory<T>
    }
}
