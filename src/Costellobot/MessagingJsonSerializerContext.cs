// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace MartinCostello.Costellobot;

[ExcludeFromCodeCoverage]
[JsonSerializable(typeof(GitHubMessage))]
public sealed partial class MessagingJsonSerializerContext : JsonSerializerContext
{
}
