// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using AspNet.Security.OAuth.GitHub;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot.Infrastructure;

public sealed class RemoteAuthorizationEventsFilter : IPostConfigureOptions<GitHubAuthenticationOptions>
{
    public RemoteAuthorizationEventsFilter(IHttpClientFactory httpClientFactory)
    {
        HttpClientFactory = httpClientFactory;
    }

    private IHttpClientFactory HttpClientFactory { get; }

    public void PostConfigure(string? name, GitHubAuthenticationOptions options)
    {
        options.Backchannel = HttpClientFactory.CreateClient(name ?? string.Empty);
        options.EventsType = typeof(LoopbackOAuthEvents);
    }
}
