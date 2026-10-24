// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace MartinCostello.Costellobot;

public sealed class GitHubMessage
{
    public static string ContentType { get; } = "application/json";

    public static string Subject { get; } = "github-webhook";

    [JsonConverter(typeof(GitHubMessageHeadersJsonConverter))]
    public required Dictionary<string, StringValues> Headers { get; init; }

    public required string Body { get; init; }
}
