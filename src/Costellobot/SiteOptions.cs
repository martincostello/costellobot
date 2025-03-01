﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class SiteOptions
{
    public IList<string> AdminRoles { get; set; } = [];

    public IList<string> AdminUsers { get; set; } = [];

    public IList<string> CrawlerPaths { get; set; } = [];

    public string LogsUrl { get; set; } = string.Empty;

    public string TelemetryCollectorUrl { get; set; } = string.Empty;
}
