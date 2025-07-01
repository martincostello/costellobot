// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit.Webhooks;

namespace MartinCostello.Costellobot.DeploymentRules;

/// <summary>
/// Defines a rule that can be used to determine whether a deployment should proceed based on a GitHub webhook event.
/// </summary>
public interface IDeploymentRule
{
    /// <summary>
    /// Gets a value indicating whether the rule is enabled.
    /// </summary>
    bool IsEnabled => true;

    /// <summary>
    /// Gets the name of the rule.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the rule for the specified webhook event.
    /// </summary>
    /// <param name="message">The GitHub webhook payload.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous evaluation of the rule
    /// which returns <see langword="true"/> if the rule is satisfied, otherwise <see langword="false"/>.
    /// </returns>
    Task<bool> EvaluateAsync(WebhookEvent message, CancellationToken cancellationToken);
}
