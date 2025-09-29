// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MartinCostello.Costellobot.Models;
using Microsoft.AspNetCore.Mvc;
using Octokit.Webhooks.AspNetCore;

namespace MartinCostello.Costellobot;

public static partial class ApiEndpoints
{
    private static readonly long StartedAt = Stopwatch.GetTimestamp();

    public static IEndpointRouteBuilder MapApiRoutes(this IEndpointRouteBuilder builder)
    {
        builder.MapGitHubWebhooks("/github-webhook");

        builder.MapGet("/badge/{type}/{owner}/{repo}", async (
            string type,
            string owner,
            string repo,
            [FromQuery(Name = "s")] string? signature,
            BadgeService service,
            HttpResponse response) =>
        {
            if (await service.GetBadgeUrlAsync(type, owner, repo, signature) is { Length: > 0 } url)
            {
                response.GetTypedHeaders().CacheControl = new() { NoCache = true };
                return Results.Redirect(url);
            }

            return Results.NotFound();
        }).AllowAnonymous();

        builder.MapGet("/badge/{type}/{owner}/{repo}.json", async (
            string type,
            string owner,
            string repo,
            [FromQuery(Name = "s")] string? signature,
            BadgeService service,
            HttpResponse response) =>
        {
            if (await service.GetBadgeAsync(type, owner, repo, signature) is { } badge)
            {
                response.GetTypedHeaders().CacheControl = new() { NoCache = true };
                return Results.Json(badge, BadgeJsonSerializerContext.Default.Badge);
            }

            return Results.NotFound();
        }).AllowAnonymous();

        builder.MapGet("/version", () => new JsonObject()
        {
            ["application"] = new JsonObject()
            {
                ["branch"] = GitMetadata.Branch,
                ["build"] = GitMetadata.BuildId,
                ["commit"] = GitMetadata.Commit,
                ["version"] = GitMetadata.Version,
            },
            ["frameworkDescription"] = RuntimeInformation.FrameworkDescription,
            ["operatingSystem"] = new JsonObject()
            {
                ["description"] = RuntimeInformation.OSDescription,
                ["architecture"] = RuntimeInformation.OSArchitecture.ToString(),
                ["version"] = Environment.OSVersion.VersionString,
                ["is64Bit"] = Environment.Is64BitOperatingSystem,
            },
            ["process"] = new JsonObject()
            {
                ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                ["is64BitProcess"] = Environment.Is64BitProcess,
                ["isNativeAoT"] = !RuntimeFeature.IsDynamicCodeSupported,
                ["isPrivilegedProcess"] = Environment.IsPrivilegedProcess,
                ["uptime"] = Stopwatch.GetElapsedTime(StartedAt).ToString(@"%d\.hh\:mm\:ss", CultureInfo.InvariantCulture),
            },
            ["dotnetVersions"] = new JsonObject()
            {
                ["runtime"] = GetVersion<object>(),
                ["aspNetCore"] = GetVersion<HttpContext>(),
            },
            ["_links"] = new JsonObject()
            {
                ["self"] = new JsonObject() { ["href"] = "https://costellobot.martincostello.com" },
                ["repo"] = new JsonObject() { ["href"] = GitMetadata.RepositoryUrl },
                ["branch"] = new JsonObject() { ["href"] = GitMetadata.BranchUrl },
                ["commit"] = new JsonObject() { ["href"] = GitMetadata.CommitUrl },
                ["deploy"] = new JsonObject() { ["href"] = GitMetadata.BuildUrl },
            },
        }).AllowAnonymous();

        return builder;

        static string GetVersion<T>()
            => typeof(T).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(Badge))]
    private sealed partial class BadgeJsonSerializerContext : JsonSerializerContext;
}
