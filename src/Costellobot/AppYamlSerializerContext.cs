// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;

namespace MartinCostello.Costellobot;

[ExcludeFromCodeCoverage]
[YamlSerializable(typeof(GitCommitAnalyzer.DependabotConfig))]
[YamlSerializable(typeof(GitCommitAnalyzer.DependabotMetadata))]
[YamlSerializable(typeof(GitCommitAnalyzer.Dependency))]
[YamlSerializable(typeof(GitCommitAnalyzer.Ignore))]
[YamlSerializable(typeof(GitCommitAnalyzer.Update))]
[YamlStaticContext]
public sealed partial class AppYamlSerializerContext;
