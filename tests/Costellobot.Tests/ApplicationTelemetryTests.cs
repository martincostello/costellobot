// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;
using OpenTelemetry;

namespace MartinCostello.Costellobot;

public static class ApplicationTelemetryTests
{
    [Fact]
    public static void HasK6Baggage_Returns_False_If_There_Is_No_Baggage()
    {
        // Arrange
        Baggage.Current = default;

        // Act
        var actual = ApplicationTelemetry.HasK6Baggage();

        // Assert
        actual.ShouldBeFalse();
    }

    [Fact]
    public static void HasK6Baggage_Returns_False_If_There_Is_No_K6_Baggage()
    {
        // Arrange
        try
        {
            Baggage.SetBaggage("other", "value");
            Baggage.SetBaggage("k6", "value");

            // Act
            var actual = ApplicationTelemetry.HasK6Baggage();

            // Assert
            actual.ShouldBeFalse();
        }
        finally
        {
            Baggage.Current = default;
        }
    }

    [Fact]
    public static void HasK6Baggage_Returns_True_If_There_Is_K6_Baggage()
    {
        // Arrange
        try
        {
            Baggage.SetBaggage("other", "value");
            Baggage.SetBaggage("k6.test_run_id", "42");

            // Act
            var actual = ApplicationTelemetry.HasK6Baggage();

            // Assert
            actual.ShouldBeTrue();
        }
        finally
        {
            Baggage.Current = default;
        }
    }

    [Fact]
    public static async Task ProfileAsync_Runs_Operation_Without_Baggage()
    {
        // Arrange
        Baggage.Current = default;

        var operations = 0;

        // Act
        await ApplicationTelemetry.ProfileAsync(
            "state",
            (state) =>
            {
                state.ShouldBe("state");
                operations++;
                return Task.CompletedTask;
            });

        // Assert
        operations.ShouldBe(1);
    }

    [Fact]
    public static async Task ProfileAsync_Runs_Operation_With_K6_Baggage()
    {
        // Arrange
        try
        {
            Baggage.SetBaggage("k6.test_run_id", "42");
            Baggage.SetBaggage("k6.empty", string.Empty);
            Baggage.SetBaggage("other", "value");

            var operations = 0;

            // Act
            await ApplicationTelemetry.ProfileAsync(
                "state",
                (state) =>
                {
                    operations++;
                    return Task.CompletedTask;
                });

            // Assert
            operations.ShouldBe(1);
        }
        finally
        {
            Baggage.Current = default;
        }
    }

    [Fact]
    public static async Task ProfileAsync_Propagates_Exceptions_From_Operation()
    {
        // Arrange
        Baggage.Current = default;

        // Act and Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => ApplicationTelemetry.ProfileAsync<object?>(
                null,
                (_) => throw new InvalidOperationException("Test")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task PyroscopeK6Middleware_Invokes_Next_Middleware(bool hasBaggage)
    {
        // Arrange
        var invocations = 0;
        var context = new DefaultHttpContext();

        var target = new PyroscopeK6Middleware(
            (context) =>
            {
                invocations++;
                return Task.CompletedTask;
            });

        try
        {
            if (hasBaggage)
            {
                Baggage.SetBaggage("k6.test_run_id", "42");
            }
            else
            {
                Baggage.Current = default;
            }

            // Act
            await target.InvokeAsync(context);

            // Assert
            invocations.ShouldBe(1);
        }
        finally
        {
            Baggage.Current = default;
        }
    }
}
