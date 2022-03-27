// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace MartinCostello.Costellobot.Infrastructure;

public class AntiforgeryTokens
{
    [JsonPropertyName("cookieName")]
    public string CookieName { get; set; } = string.Empty;

    [JsonPropertyName("cookieValue")]
    public string? CookieValue { get; set; } = string.Empty;

    [JsonPropertyName("formFieldName")]
    public string? FormFieldName { get; set; } = string.Empty;

    [JsonPropertyName("headerName")]
    public string HeaderName { get; set; } = string.Empty;

    [JsonPropertyName("requestToken")]
    public string? RequestToken { get; set; } = string.Empty;
}
