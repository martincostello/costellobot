// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;

namespace MartinCostello.Costellobot.Pages;

public sealed class TokenBrokerProfileItem : AppPage.Item
{
    internal TokenBrokerProfileItem(IElementHandle handle, IPage page)
        : base(handle, page)
    {
    }

    public async Task<string> NameAsync()
        => await StringAsync(".token-broker-profile-name");

    public async Task<string> TypeAsync()
        => await StringAsync(".token-broker-profile-type");
}
