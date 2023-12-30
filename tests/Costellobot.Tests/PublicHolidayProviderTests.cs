// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Time.Testing;

namespace MartinCostello.Costellobot;

public class PublicHolidayProviderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("2023-12-24T12:34:56", false)] // Christmas Eve
    [InlineData("2023-12-25T12:34:56", true)] // Christmas Day
    [InlineData("2023-12-26T12:34:56", true)] // Boxing Day
    [InlineData("2023-12-27T12:34:56", false)] // A normal day
    [InlineData("2024-01-01T12:34:56", true)] // New Year's Day
    [InlineData("2024-01-02T12:34:56", false)] // A normal day
    public void IsPublicHoliday_Returns_Correct_Value(string utcNowString, bool expected)
    {
        // Arrange
        PublicHolidayProvider target = CreateTarget(utcNowString);

        // Act
        bool actual = target.IsPublicHoliday();

        // Assert
        actual.ShouldBe(expected);
    }

    private PublicHolidayProvider CreateTarget(string utcNowString)
    {
        var utcNow = DateTimeOffset.Parse(utcNowString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        var timeProvider = new FakeTimeProvider(utcNow);

        return new(
            timeProvider,
            outputHelper.ToLogger<PublicHolidayProvider>());
    }
}
