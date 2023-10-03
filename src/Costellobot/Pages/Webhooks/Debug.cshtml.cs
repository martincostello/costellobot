// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot.Pages;

[CostellobotAdmin]
public sealed partial class DebugModel(IOptions<GitHubOptions> options) : PageModel
{
    public string AppId { get; } = options.Value.AppId;

    public bool RequireSignature { get; } = !string.IsNullOrWhiteSpace(options.Value.WebhookSecret);

    public void OnGet()
    {
        // No-op
    }
}
