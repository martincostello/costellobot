// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Octokit;

namespace MartinCostello.Costellobot.Pages;

[CostellobotAdmin]
public sealed partial class DeliveriesModel(IGitHubClientForApp client) : PageModel
{
    public IList<WebhookDelivery> Deliveries { get; } = new List<WebhookDelivery>();

    [BindProperty]
    public Guid? DeliveryId { get; set; }

    public async Task OnGet()
    {
        (var deliveries, _) = await GetDeliveries(cursor: null);

        foreach (var item in deliveries)
        {
            Deliveries.Add(item);
        }
    }

    public async Task<IActionResult> OnPost()
    {
        const int MaxPages = 20;

        string? cursor = null;
        string? deliveryId = DeliveryId?.ToString();

        if (deliveryId is { } guid)
        {
            for (int i = 0; i < MaxPages; i++)
            {
                (var deliveries, cursor) = await GetDeliveries(cursor);

                var item = deliveries.FirstOrDefault((p) => p.Guid == guid);

                if (item is not null)
                {
                    return RedirectToPage("Delivery", new { id = item.Id });
                }
            }
        }

        return RedirectToPage("Deliveries");
    }

    private static string? ExtractCursor(IReadOnlyDictionary<string, string> headers)
    {
        string? cursor = null;

        if (headers.TryGetValue("Link", out var link))
        {
            var nextUrl = link
                .Split(',')
                .Where((p) => p.EndsWith("; rel=\"next\"", StringComparison.Ordinal))
                .FirstOrDefault();

            if (nextUrl is not null)
            {
                var url = nextUrl.Split(';')[0];
                url = url.Trim('<', '>');

                var query = QueryHelpers.ParseQuery(url);

                if (query.TryGetValue("cursor", out var value))
                {
                    cursor = value.ToString();
                }
            }
        }

        return cursor;
    }

    private async Task<(IReadOnlyList<WebhookDelivery> Deliveries, string? Cursor)> GetDeliveries(string? cursor = null)
    {
        // See https://docs.github.com/en/rest/apps/webhooks#list-deliveries-for-an-app-webhook
        var uri = new Uri("app/hook/deliveries", UriKind.Relative);

        var parameters = new Dictionary<string, string>(2)
        {
            ["per_page"] = "100",
        };

        if (cursor is not null)
        {
            parameters["cursor"] = cursor;
        }

        var response = await client.Connection.Get<List<WebhookDelivery>>(
            uri,
            parameters,
            "application/vnd.github+json");

        if (response.HttpResponse.StatusCode is not HttpStatusCode.OK)
        {
            return ([], null);
        }

        var deliveries = response.Body;
        var next = ExtractCursor(response.HttpResponse.Headers);

        return (deliveries, next);
    }
}
