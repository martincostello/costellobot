// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace MartinCostello.Costellobot.Models;

public sealed class GitHubTokenResponse
{
    [JsonPropertyName("token")]
    public required string Token { get; set; }

    [JsonPropertyName("type")]
    public required string TokenType { get; set; }

    [JsonPropertyName("appId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? AppId { get; set; }

    [JsonPropertyName("appSlug")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AppSlug { get; set; }

    [JsonPropertyName("installationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? InstallationId { get; set; }
}
