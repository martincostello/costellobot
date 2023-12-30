// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MartinCostello.Costellobot;

public sealed partial class PublicHolidayProvider(
    TimeProvider timeProvider,
    ILogger<PublicHolidayProvider> logger)
{
    private static readonly BankHolidays UKBankHolidays = LoadUKBankHolidays();

    public bool IsPublicHoliday()
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return IsBankHoliday(today);
    }

    private static BankHolidays LoadUKBankHolidays()
    {
        // The original data was obtained from https://www.gov.uk/bank-holidays.json
        using var stream = typeof(PublicHolidayProvider).Assembly.GetManifestResourceStream("MartinCostello.Costellobot.bank-holidays.json");
        return JsonSerializer.Deserialize(stream!, HolidaysJsonSerializerContext.Default.BankHolidays) ?? new();
    }

    private bool IsBankHoliday(DateOnly today)
    {
        var bankHoliday = UKBankHolidays.EnglandAndWales?.Events?
            .Where((p) => p.Date == today)
            .FirstOrDefault();

        if (bankHoliday is not null)
        {
            Log.TodayIsBankHoliday(logger, bankHoliday.Title);
            return true;
        }

        return false;
    }

    [ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Today is the {BankHolidayTitle} bank holiday in England and Wales.")]
        public static partial void TodayIsBankHoliday(ILogger logger, string bankHolidayTitle);
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(BankHolidays))]
    private sealed partial class HolidaysJsonSerializerContext : JsonSerializerContext
    {
    }

    private sealed class BankHolidays
    {
        [JsonPropertyName("england-and-wales")]
        public BankHolidayDivision EnglandAndWales { get; set; } = null!;
    }

    private sealed class BankHolidayDivision
    {
        [JsonPropertyName("division")]
        public string Division { get; set; } = null!;

        [JsonPropertyName("events")]
        public List<BankHolidayEvent> Events { get; set; } = [];
    }

    private sealed class BankHolidayEvent
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = null!;

        [JsonPropertyName("date")]
        public DateOnly Date { get; set; }
    }
}
