// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Octokit;

namespace MartinCostello.Costellobot.Pages;

[CostellobotAdmin]
public sealed partial class DeliveryModel(IGitHubClientForApp client) : PageModel
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    [BindProperty]
    public long Id { get; set; }

    public string Action => Delivery.GetProperty("action").GetString() ?? "-";

    public string DeliveryId => Delivery.GetProperty("guid").GetString() ?? "-";

    public DateTimeOffset DeliveredAt => Delivery.GetProperty("delivered_at").GetDateTimeOffset();

    public string Duration => Delivery
        .GetProperty("duration")
        .GetDouble()
        .Seconds()
        .TotalSeconds
        .ToString("N2", CultureInfo.InvariantCulture);

    public string Event => Delivery.GetProperty("event").GetString() ?? "-";

    public bool Redelivery => Delivery.GetProperty("redelivery").GetBoolean();

    public string RepositoryId { get; set; } = "-";

    public IDictionary<string, string> RequestHeaders { get; } = new Dictionary<string, string>();

    public string RequestPayload { get; set; } = string.Empty;

    public string RequestUrl => Delivery.GetProperty("url").GetString() ?? "-";

    public IDictionary<string, string> ResponseHeaders { get; } = new Dictionary<string, string>();

    public string ResponseBody { get; set; } = string.Empty;

    public string? ResponseStatus => Delivery.GetProperty("status").GetString();

    public int ResponseStatusCode => Delivery.GetProperty("status_code").GetInt32();

    private JsonElement Delivery { get; set; }

    public async Task<IActionResult> OnGet([FromRoute(Name = "id")] long id)
    {
        // See https://docs.github.com/en/rest/apps/webhooks#get-a-delivery-for-an-app-webhook
        var uri = new Uri($"app/hook/deliveries/{id}", UriKind.Relative);

        IApiResponse<Stream> apiResponse;

        try
        {
            apiResponse = await client.Connection.GetRawStream(uri, null);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        Delivery = JsonDocument.Parse(apiResponse.HttpResponse!.Body!.ToString()!).RootElement;

        // TODO Use Body directly when https://github.com/octokit/octokit.net/pull/2791 available
        ////Delivery = (await JsonDocument.ParseAsync(apiResponse.Body)).RootElement;

        Id = Delivery.GetProperty("id").GetInt64();

        var request = Delivery.GetProperty("request");

        TryPopulateHeaders(request.GetProperty("headers"), RequestHeaders);

        RequestPayload = JsonSerializer.Serialize(
            request.GetProperty("payload"),
            IndentedOptions);

        var response = Delivery.GetProperty("response");

        TryPopulateHeaders(response.GetProperty("headers"), ResponseHeaders);

        ResponseBody = response.GetProperty("payload").ToString();

        if (Delivery.TryGetProperty("repository_id", out var repositoryId) &&
            repositoryId.ValueKind != JsonValueKind.Null &&
            repositoryId.TryGetInt64(out var value))
        {
            RepositoryId = value.ToString(CultureInfo.InvariantCulture);
        }

        return Page();

        static void TryPopulateHeaders(JsonElement element, IDictionary<string, string> headers)
        {
            if (element.ValueKind != JsonValueKind.Null)
            {
                foreach (var property in element.EnumerateObject().OrderBy((p) => p.Name, StringComparer.OrdinalIgnoreCase))
                {
                    headers[property.Name] = property.Value.ToString();
                }
            }
        }
    }

    public async Task<IActionResult> OnPost()
    {
        // See https://docs.github.com/en/rest/apps/webhooks#redeliver-a-delivery-for-an-app-webhook
        var uri = new Uri($"app/hook/deliveries/{Id}/attempts", UriKind.Relative);

        await client.Connection.Post(uri);

        return RedirectToPage("Deliveries");
    }
}
