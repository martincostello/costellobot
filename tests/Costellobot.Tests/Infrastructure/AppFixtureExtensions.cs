// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot.Infrastructure;

public static class AppFixtureExtensions
{
    public static T ApproveDeployments<T>(this T fixture, bool enabled = true)
        where T : AppFixture
    {
        fixture.OverrideConfiguration("Webhook:Deploy", enabled.ToString());
        return fixture;
    }

    public static T ApprovePullRequests<T>(this T fixture, bool enabled = true)
        where T : AppFixture
    {
        fixture.OverrideConfiguration("Webhook:Approve", enabled.ToString());
        return fixture;
    }

    public static T AutoMergeEnabled<T>(this T fixture, bool enabled = true)
        where T : AppFixture
    {
        fixture.OverrideConfiguration("Webhook:Automerge", enabled.ToString());
        return fixture;
    }

    public static T FailedCheckRerunAttempts<T>(this T fixture, int value)
        where T : AppFixture
    {
        fixture.OverrideConfiguration("Webhook:RerunFailedChecksAttempts", value.ToString(CultureInfo.InvariantCulture));
        return fixture;
    }
}
