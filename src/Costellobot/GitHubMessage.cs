// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class GitHubMessage
{
    public static string ContentType { get; } = "application/json";

    public static string Subject { get; } = "github-webhook";

    public required Dictionary<string, string?[]?> Headers { get; init; }

    public required string Body { get; init; }
}
