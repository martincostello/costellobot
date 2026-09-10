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

    /// <inheritdoc/>
    public override string Name => "Not-Busy-Calendar";

    /// <inheritdoc/>
    public override async Task<bool> EvaluateAsync(WebhookEvent message, CancellationToken cancellationToken)
    {
        if (options.CurrentValue.CalendarIds is { Count: > 0 } calendarIds)
        {
            var timeZoneId = options.CurrentValue.CalendarTimeZoneId;
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var today = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).Date;

            foreach (var calendarId in calendarIds.Where((p) => !string.IsNullOrEmpty(p)))
            {
                var events = await cache.GetOrCreateAsync(
                    CacheKey(today, calendarId),
                    (calendar, today, timeZone, timeZoneId, calendarId),
                    static async (state, cancellationToken) =>
                    {
                        var (calendar, today, timeZone, timeZoneId, calendarId) = state;

                        var minTime = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(today, timeZone), TimeSpan.Zero);
                        var maxTime = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(today.AddDays(1), timeZone), TimeSpan.Zero);

                        var request = calendar.Events.List(calendarId);

                        request.Fields = "items(start,end,summary,transparency,eventType)";
                        request.SingleEvents = true;
                        request.TimeMinDateTimeOffset = minTime;
                        request.TimeMaxDateTimeOffset = maxTime;
                        request.TimeZone = timeZoneId;

                        return await request.ExecuteAsync(cancellationToken);
                    },
                    CacheEntryOptions,
                    CacheTags,
                    cancellationToken);

                var @event = events.Items.FirstOrDefault((item) => IsBusy(item, timeZone));

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

        static bool IsBusy(Event @event, TimeZoneInfo timeZone)
        {
            var isAllDayEvent =
                (@event.Start.Date is not null && @event.End.Date is not null) ||
                IsFullLocalDay(@event.Start.DateTimeDateTimeOffset, @event.End.DateTimeDateTimeOffset, timeZone);

            if (!isAllDayEvent)
            {
                return false;
            }

            return @event.Transparency is not "transparent" ||
                   @event.EventType is "outOfOffice";
        }

        static bool IsFullLocalDay(DateTimeOffset? start, DateTimeOffset? end, TimeZoneInfo timeZone)
        {
            if (start is not { } startValue || end is not { } endValue)
            {
                return false;
            }

            var startLocal = TimeZoneInfo.ConvertTime(startValue, timeZone);
            var endLocal = TimeZoneInfo.ConvertTime(endValue, timeZone);

            return startLocal.TimeOfDay == TimeSpan.Zero &&
                   endLocal.TimeOfDay == TimeSpan.Zero &&
                   endLocal.Date == startLocal.Date.AddDays(1);
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
