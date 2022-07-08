// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot.Pages;

[CostellobotAdmin]
public sealed partial class DebugModel : PageModel
{
    public DebugModel(IOptions<GitHubOptions> options)
    {
        AppId = options.Value.AppId;
        RequireSignature = !string.IsNullOrWhiteSpace(options.Value.WebhookSecret);
    }

    public string AppId { get; }

    public bool RequireSignature { get; }

    public void OnGet()
    {
        // No-op
    }
}
