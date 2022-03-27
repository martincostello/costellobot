// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot.Infrastructure;

public sealed class AntiforgeryTokenController : Controller
{
    private const string GetUrl = "_testing/get-xsrf-token";

    public static Uri GetTokensUri { get; } = new Uri(GetUrl, UriKind.Relative);

    [AllowAnonymous]
    [HttpGet]
    [IgnoreAntiforgeryToken]
    [Route(GetUrl, Name = "GetAntiforgeryTokens")]
    public IActionResult GetAntiforgeryTokens(
        [FromServices] IAntiforgery antiforgery,
        [FromServices] IOptions<AntiforgeryOptions> options)
    {
        AntiforgeryTokenSet tokens = antiforgery.GetTokens(HttpContext);

        var model = new AntiforgeryTokens()
        {
            CookieName = options.Value!.Cookie!.Name!,
            CookieValue = tokens.CookieToken!,
            FormFieldName = options.Value.FormFieldName,
            HeaderName = tokens.HeaderName!,
            RequestToken = tokens.RequestToken!,
        };

        return Json(model);
    }
}
