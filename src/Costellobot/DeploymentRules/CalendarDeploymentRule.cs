// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Google.Apis.Calendar.v3;
using Microsoft.Extensions.Options;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot.DeploymentRules;

public sealed partial class CalendarDeploymentRule(
    CalendarService calendar,
    TimeProvider timeProvider,
    IOptionsMonitor<GoogleOptions> options,
    ILogger<CalendarDeploymentRule> logger) : DeploymentRule
{
    /// <inheritdoc/>
    public override string Name => "Not-Busy-Calendar";

    /// <inheritdoc/>
    public override async Task<bool> EvaluateAsync(WebhookEvent message, CancellationToken cancellationToken)
    {
        if (options.CurrentValue.CalendarId is { Length: > 0 } calendarId)
        {
            var today = timeProvider.GetUtcNow().Date;
            var minTime = new DateTimeOffset(today, TimeSpan.Zero);
            var maxTime = minTime.AddDays(1);

            var request = calendar.Events.List(calendarId);

            request.Fields = "items(start,end,summary,transparency)";
            request.SingleEvents = true;
            request.TimeMinDateTimeOffset = minTime;
            request.TimeMaxDateTimeOffset = maxTime;

            var events = await request.ExecuteAsync(cancellationToken);

            // Filter for all-day events that are marked as busy
            var @event = events.Items.FirstOrDefault(
                (p) => p.Start.Date is not null &&
                       p.End.Date is not null &&
                       p.Transparency is not "transparent");

            if (@event is not null)
            {
                Log.CalendarOwnerIsBusy(logger, @event.Summary ?? "unknown");
                return false;
            }
        }

        return true;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Deployment is not approved as calendar suggests owner is busy all day with {EventName} event.")]
        public static partial void CalendarOwnerIsBusy(ILogger logger, string eventName);
    }
}
