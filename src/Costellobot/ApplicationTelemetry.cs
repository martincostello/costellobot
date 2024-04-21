// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace MartinCostello.Costellobot;

public static class ApplicationTelemetry
{
    public static readonly string ServiceName = "Costellobot";
    public static readonly string ServiceVersion = GitMetadata.Version.Split('+')[0];
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);
}
