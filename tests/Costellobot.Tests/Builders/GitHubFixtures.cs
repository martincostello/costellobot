// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Builders;

public static class GitHubFixtures
{
    public const string AuthorizationHeader = "Token ghs_secret-access-token";

    public const string DependabotBotName = "app/dependabot";

    public const string GitHubActionsBotName = "app/github-actions";

    public const string InstallationId = "42";

    public static AccessTokenBuilder CreateAccessToken(string? token = null)
    {
        var builder = new AccessTokenBuilder();

        if (token is not null)
        {
            builder.Token = token;
        }

        return builder;
    }

    public static IssueCommentBuilder CreateIssueComment(string login, string body)
    {
        var user = new UserBuilder(login);
        var builder = new IssueCommentBuilder(user);

        if (body is not null)
        {
            builder.Body = body;
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
