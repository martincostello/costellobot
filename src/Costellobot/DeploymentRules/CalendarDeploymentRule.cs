// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Google.Apis.Calendar.v3;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Octokit.Webhooks;
using Event = Google.Apis.Calendar.v3.Data.Event;

namespace MartinCostello.Costellobot.DeploymentRules;

public sealed partial class CalendarDeploymentRule(
    CalendarService calendar,
    HybridCache cache,
    TimeProvider timeProvider,
    IOptionsMonitor<GoogleOptions> options,
    ILogger<CalendarDeploymentRule> logger) : DeploymentRule
{
    private static readonly HybridCacheEntryOptions CacheEntryOptions = new() { Expiration = TimeSpan.FromHours(3) };
    private static readonly string[] CacheTags = ["all", "calendar"];
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    /// <inheritdoc/>
    public override string Name => "Not-Busy-Calendar";

    /// <inheritdoc/>
    public override async Task<bool> EvaluateAsync(WebhookEvent message, CancellationToken cancellationToken)
    {
        if (options.CurrentValue.CalendarIds is { Count: > 0 } calendarIds)
        {
            var today = timeProvider.GetUtcNow().Date;

            foreach (var calendarId in calendarIds)
            {
                var events = await cache.GetOrCreateAsync(
                    CacheKey(today, calendarId),
                    (calendar, today, calendarId),
                    static async (state, cancellationToken) =>
                    {
                        var (calendar, today, calendarId) = state;

                        var minTime = new DateTimeOffset(today, TimeSpan.Zero);
                        var maxTime = minTime.AddDays(1);

                        var request = calendar.Events.List(calendarId);

                        request.Fields = "items(start,end,summary,transparency,eventType)";
                        request.SingleEvents = true;
                        request.TimeMinDateTimeOffset = minTime;
                        request.TimeMaxDateTimeOffset = maxTime;

                        return await request.ExecuteAsync(cancellationToken);
                    },
                    CacheEntryOptions,
                    CacheTags,
                    cancellationToken);

                var @event = events.Items.FirstOrDefault(IsBusy);

                if (@event is not null)
                {
                    Log.CalendarOwnerIsBusy(logger, @event.Summary ?? "unknown");
                    return false;
                }
            }
        }

        return true;

        static string CacheKey(DateTime date, string calendarId)
        {
            var hash = calendarId.GetHashCode(StringComparison.Ordinal);
            return FormattableString.Invariant($"calendar:{date:d}:{hash}");
        }

        static bool IsBusy(Event @event)
        {
            var isAllDayEvent =
                (@event.Start.Date is not null && @event.End.Date is not null) ||
                ((@event.End.DateTimeDateTimeOffset - @event.Start.DateTimeDateTimeOffset) == OneDay);

            if (!isAllDayEvent)
            {
                return false;
            }

            return @event.Transparency is not "transparent" ||
                   @event.EventType is "outOfOffice";
        }
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
