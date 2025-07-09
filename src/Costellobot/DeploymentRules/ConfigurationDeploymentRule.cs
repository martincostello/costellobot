// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot.DeploymentRules;

public sealed partial class ConfigurationDeploymentRule(
    IOptionsMonitor<WebhookOptions> options,
    ILogger<ConfigurationDeploymentRule> logger) : DeploymentRule
{
    /// <inheritdoc/>
    public override string Name => "Enabled-By-Application-Configuration";

    /// <inheritdoc/>
    public override Task<bool> EvaluateAsync(WebhookEvent message, CancellationToken cancellationToken)
    {
        var result = options.CurrentValue.Deploy;

        if (!result)
        {
            Log.DeploymentApprovalIsDisabled(logger);
        }

        return Task.FromResult(result);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Deployment is not approved as it is disabled in application configuration.")]
        public static partial void DeploymentApprovalIsDisabled(ILogger logger);
    }
}
