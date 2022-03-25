// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Primitives;
using Terrajobst.GitHubEvents;

namespace MartinCostello.Costellobot.Builders;

public static class GitHubFixtures
{
    public const string AuthorizationHeader = "Token ghs_secret-access-token";

    public const string DependabotBotName = "app/dependabot";

    public const string GitHubActionsBotName = "app/github-actions";

    public const string InstallationId = "42";

    public static GitHubEvent CreateEvent(
        string @event,
        object? payload = null,
        string? installationId = null)
    {
        var headers = new Dictionary<string, StringValues>()
        {
            ["Accept"] = "*/*",
            ["User-Agent"] = "GitHub-Hookshot/f05835d",
            ["X-GitHub-Delivery"] = Guid.NewGuid().ToString(),
            ["X-GitHub-Event"] = @event,
            ["X-GitHub-Hook-ID"] = Guid.NewGuid().ToString(),
            ["X-GitHub-Hook-Installation-Target-ID"] = installationId ?? InstallationId,
            ["X-GitHub-Hook-Installation-Target-Type"] = "integration",
        };

        string body = JsonSerializer.Serialize(payload ?? new { });

        return GitHubEvent.Parse(headers, body);
    }

    public static AccessTokenBuilder CreateAccessToken(string? token = null)
    {
        var builder = new AccessTokenBuilder();

        if (token is not null)
        {
            builder.Token = token;
        }

        return builder;
    }

    public static UserBuilder CreateUser(
        string? login = null,
        int? id = null,
        string? userType = null)
    {
        UserBuilder builder = new(login);

        if (id is { } identifier)
        {
            builder.Id = identifier;
        }

        if (userType is not null)
        {
            builder.UserType = userType;
        }

        return builder;
    }
}
