// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit.Webhooks;

namespace MartinCostello.Costellobot.DeploymentRules;

public sealed partial class PublicHolidayDeploymentRule(
    PublicHolidayProvider provider,
    ILogger<PublicHolidayDeploymentRule> logger) : IDeploymentRule
{
    /// <inheritdoc/>
    public string Name => "Not-A-UK-Public-Holiday";

    /// <inheritdoc/>
    public Task<bool> EvaluateAsync(WebhookEvent message, CancellationToken cancellationToken)
    {
        if (message is Octokit.Webhooks.Events.DeploymentProtectionRule.DeploymentProtectionRuleRequestedEvent)
        {
            return Task.FromResult(true);
        }

        var result = provider.IsPublicHoliday();

        if (result)
        {
            Log.TodayIsAPublicHoliday(logger);
        }

        return Task.FromResult(!result);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Information,
           Message = "Deployment is not approved as today is a public holiday.")]
        public static partial void TodayIsAPublicHoliday(ILogger logger);
    }
}
