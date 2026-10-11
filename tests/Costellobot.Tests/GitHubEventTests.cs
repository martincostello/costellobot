// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Primitives;
using Octokit.Webhooks;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot;

public static class GitHubEventTests
{
    [Fact]
    public static void RawPayload_Is_Parsed_From_Body_If_Not_Specified()
    {
        // Arrange
        string body =
            """
            {
              "zen": "Responsive is better than fast.",
              "hook_id": 109948940
            }
            """;

        var headers = CreateHeaders();
        var rawHeaders = new Dictionary<string, string>();

        var target = new GitHubEvent(headers, CreatePingEvent(), rawHeaders, body);

        // Act
        var actual = target.RawPayload;

        // Assert
        actual.ValueKind.ShouldBe(JsonValueKind.Object);
        actual.GetProperty("zen").GetString().ShouldBe("Responsive is better than fast.");
        actual.GetProperty("hook_id").GetInt32().ShouldBe(109948940);

        // Act
        var second = target.RawPayload;

        // Assert
        second.GetRawText().ShouldBe(actual.GetRawText());

        target.Body.ShouldBe(body);
        target.Headers.ShouldBeSameAs(headers);
        target.RawHeaders.ShouldBeSameAs(rawHeaders);
        target.ToString().ShouldBe("ping");
    }

    [Fact]
    public static void RawPayload_Is_Used_If_Specified()
    {
        // Arrange
        string body = """{"zen":"Keep it logically awesome."}""";

        var headers = CreateHeaders();
        var rawHeaders = new Dictionary<string, string>();
        var rawPayload = JsonElement.Parse(body);

        var target = new GitHubEvent(headers, CreatePingEvent(), rawHeaders, body, rawPayload);

        // Act
        var actual = target.RawPayload;

        // Assert
        actual.GetRawText().ShouldBe(rawPayload.GetRawText());
        actual.GetProperty("zen").GetString().ShouldBe("Keep it logically awesome.");
        target.Body.ShouldBe(body);
    }

    private static WebhookHeaders CreateHeaders()
    {
        var headers = new Dictionary<string, StringValues>()
        {
            ["X-GitHub-Delivery"] = Guid.NewGuid().ToString(),
            ["X-GitHub-Event"] = "ping",
        };

        return WebhookHeaders.Parse(headers);
    }
}
