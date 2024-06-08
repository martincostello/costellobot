// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Octokit.Webhooks.AspNetCore;

namespace MartinCostello.Costellobot;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiRoutes(this IEndpointRouteBuilder builder, IConfiguration configuration)
    {
        builder.MapGitHubWebhooks("/github-webhook", configuration["GitHub:WebhookSecret"] ?? string.Empty);

        builder.MapGet("/badge/{type}/{owner}/{repo}", async (string type, string owner, string repo, [FromQuery(Name = "s")] string? signature, BadgeService service) =>
        {
            if (await service.GetBadgeAsync(type, owner, repo, signature) is { } url)
            {
                return Results.Redirect(url);
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
            },
            ["dotnetVersions"] = new JsonObject()
            {
                ["runtime"] = GetVersion<object>(),
                ["aspNetCore"] = GetVersion<HttpContext>(),
            },
            ["_links"] = new JsonObject()
            {
                ["self"] = new JsonObject() { ["href"] = "https://costellobot.martincostello.com" },
                ["repo"] = new JsonObject() { ["href"] = "https://github.com/martincostello/costellobot" },
                ["branch"] = new JsonObject() { ["href"] = $"https://github.com/martincostello/costellobot/tree/{GitMetadata.Branch}" },
                ["commit"] = new JsonObject() { ["href"] = $"https://github.com/martincostello/costellobot/commit/{GitMetadata.Commit}" },
                ["deploy"] = new JsonObject() { ["href"] = $"https://github.com/martincostello/costellobot/actions/runs/{GitMetadata.BuildId}" },
            },
        }).AllowAnonymous();

        return builder;

        static string GetVersion<T>()
            => typeof(T).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    }
}
