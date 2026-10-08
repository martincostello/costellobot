// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;

namespace Octokit;

public sealed class AsyncMergeResponse
{
    public HttpStatusCode StatusCode { get; set; }

    public string? Status { get; set; }

    public AsyncMergeDetails? Details { get; set; }
}
