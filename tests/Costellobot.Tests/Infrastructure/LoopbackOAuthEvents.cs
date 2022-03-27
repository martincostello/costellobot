// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace MartinCostello.Costellobot.Infrastructure;

public sealed class LoopbackOAuthEvents : OAuthEvents
{
    public override Task RedirectToAuthorizationEndpoint(RedirectContext<OAuthOptions> context)
    {
        var query = new UriBuilder(context.RedirectUri).Uri.Query;
        var queryString = HttpUtility.ParseQueryString(query);

        var location = queryString["redirect_uri"];
        var state = queryString["state"];

        queryString.Clear();

        var code = Guid.NewGuid().ToString();

        queryString.Add("code", code);
        queryString.Add("state", state);

        var builder = new UriBuilder(location!)
        {
            Query = queryString.ToString() ?? string.Empty,
        };

        context.RedirectUri = builder.ToString();

        return base.RedirectToAuthorizationEndpoint(context);
    }
}
