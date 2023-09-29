// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using AspNet.Security.OAuth.GitHub;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot.Infrastructure;

public sealed class RemoteAuthorizationEventsFilter(IHttpClientFactory httpClientFactory) : IPostConfigureOptions<GitHubAuthenticationOptions>
{
    public void PostConfigure(string? name, GitHubAuthenticationOptions options)
    {
        options.Backchannel = httpClientFactory.CreateClient(name ?? string.Empty);
        options.EventsType = typeof(LoopbackOAuthEvents);
    }
}
