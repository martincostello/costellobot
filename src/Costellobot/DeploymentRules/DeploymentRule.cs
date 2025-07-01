// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit.Webhooks;

namespace MartinCostello.Costellobot.DeploymentRules;

/// <summary>
/// Defines a rule that can be used to determine whether a deployment should proceed based on a GitHub webhook event.
/// </summary>
public abstract class DeploymentRule : IDeploymentRule
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    public static async Task<(bool Approved, string? DeniedRuleName)> EvaluateAsync(
        IEnumerable<IDeploymentRule> rules,
        WebhookEvent message,
        CancellationToken cancellationToken)
    {
        foreach (var rule in rules.Where((p) => p.IsEnabled))
        {
            if (!await rule.EvaluateAsync(message, cancellationToken))
            {
                return (false, rule.Name);
            }
        }

        return (true, null);
    }

    /// <inheritdoc/>
    public abstract Task<bool> EvaluateAsync(WebhookEvent message, CancellationToken cancellationToken);
}
