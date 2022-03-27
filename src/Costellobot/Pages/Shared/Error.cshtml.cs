// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable SA1649

using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MartinCostello.Costellobot.Pages;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ErrorModel : PageModel
{
    public int ErrorStatusCode { get; private set; } = StatusCodes.Status500InternalServerError;

    public bool IsClientError { get; private set; }

    public string Message { get; set; } = "Sorry, something went wrong.";

    public string? RequestId { get; private set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public string Title { get; set; } = "Error";

    public string Subtitle { get; set; } = "Error";

    public void OnGet(int? id = null)
    {
        int httpCode = id ?? StatusCodes.Status500InternalServerError;

        if (!Enum.IsDefined(typeof(HttpStatusCode), (HttpStatusCode)httpCode) ||
            id < StatusCodes.Status400BadRequest ||
            id > 599)
        {
            httpCode = StatusCodes.Status500InternalServerError;
        }

        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        Subtitle = $"Error (HTTP {httpCode})";

        switch (httpCode)
        {
            case StatusCodes.Status400BadRequest:
                Title = "Bad request";
                Subtitle = "Bad request (HTTP 400)";
                Message = "The request was invalid.";
                IsClientError = true;
                break;

            case StatusCodes.Status401Unauthorized:
                Title = "Unauthorized";
                Subtitle = "Unauthorized (HTTP 401)";
                Message = "You must authenticate to access this page.";
                IsClientError = true;
                break;

            case StatusCodes.Status403Forbidden:
                Title = "Forbidden";
                Subtitle = "Forbidden (HTTP 403)";
                Message = "You are not permitted to access this page.";
                IsClientError = true;
                break;

            case StatusCodes.Status404NotFound:
                Title = "Not found";
                Subtitle = "Page not found (HTTP 404)";
                Message = "The page you requested could not be found.";
                IsClientError = true;
                break;

            case StatusCodes.Status405MethodNotAllowed:
                Title = "Method not allowed";
                Subtitle = "HTTP method not allowed (HTTP 405)";
                Message = "The specified HTTP method was not allowed.";
                IsClientError = true;
                break;

            default:
                break;
        }

        Response.StatusCode = httpCode;
    }
}
