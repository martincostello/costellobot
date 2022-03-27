// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable SA1649

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MartinCostello.Costellobot.Pages;

[CostellobotAdmin]
public sealed class IndexModel : PageModel
{
    public void OnGet()
    {
        // No-op
    }
}
