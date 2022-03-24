// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable SA1516

using System.Net;
using System.Reflection;
using MartinCostello.Costellobot;
using Microsoft.AspNetCore.Http.Json;
using Terrajobst.GitHubEvents.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGitHub(builder.Configuration);

builder.Services.Configure<JsonOptions>((options) =>
{
    options.SerializerOptions.WriteIndented = true;
});

builder.Host.ConfigureApplication();

builder.WebHost.ConfigureKestrel((p) => p.AddServerHeader = false);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseMiddleware<CustomHttpHeadersMiddleware>();

app.UseStatusCodePagesWithReExecute("/error", "?id={0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.MapGet("/", () => Results.Redirect("https://martincostello.com/"));

app.MapGet("/version", () => new
{
    branch = GitMetadata.Branch,
    build = GitMetadata.BuildId,
    commit = GitMetadata.Commit,
    version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
    _links = new
    {
        self = new { href = "https://costellobot.martincostello.com" },
        repo = new { href = "https://github.com/martincostello/costellobot" },
    },
});

var allMethods = new[]
{
    HttpMethods.Connect,
    HttpMethods.Delete,
    HttpMethods.Get,
    HttpMethods.Head,
    HttpMethods.Options,
    HttpMethods.Patch,
    HttpMethods.Post,
    HttpMethods.Put,
    HttpMethods.Trace,
};

app.MapMethods("/error", allMethods, (
    int? id,
    HttpContext httpContext,
    IWebHostEnvironment environment) =>
{
    int httpCode = id ?? StatusCodes.Status500InternalServerError;

    if (!Enum.IsDefined(typeof(HttpStatusCode), (HttpStatusCode)httpCode) ||
        id < StatusCodes.Status400BadRequest ||
        id > 599)
    {
        httpCode = StatusCodes.Status500InternalServerError;
    }

    string fileName = httpCode switch
    {
        StatusCodes.Status400BadRequest => "bad-request.html",
        StatusCodes.Status404NotFound => "not-found.html",
        _ => "error.html",
    };

    var fileInfo = environment.WebRootFileProvider.GetFileInfo(fileName);
    var stream = fileInfo.CreateReadStream();

    httpContext.Response.StatusCode = httpCode;

    return Results.Stream(stream, "text/html");
});

app.MapGitHubWebHook(secret: app.Configuration["GitHub:WebhookSecret"]);

app.Run();

namespace MartinCostello.Costellobot
{
    public partial class Program
    {
        // Expose the Program class for use with WebApplicationFactory<T>
    }
}
