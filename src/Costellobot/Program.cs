// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable SA1516

using MartinCostello.Costellobot;
using Terrajobst.GitHubEvents;
using Terrajobst.GitHubEvents.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGitHubEventProcessor, GitHubEventProcessor>();

builder.Host.ConfigureApplication();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapGitHubWebHook(secret: app.Configuration["GitHub:WebhookSecret"]);

app.Run();
