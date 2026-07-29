// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using MartinCostello.Costellobot.Models;

namespace MartinCostello.Costellobot;

[ExcludeFromCodeCoverage]
[JsonSerializable(typeof(ClientLogMessage))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(GitHubTokenRequest))]
[JsonSerializable(typeof(GitHubTokenResponse))]
[JsonSerializable(typeof(JsonElement))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext;
