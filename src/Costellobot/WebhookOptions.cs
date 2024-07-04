// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class WebhookOptions
{
    public bool Approve { get; set; }

    public string ApproveComment { get; set; } = string.Empty;

    public IList<string> ApproveLabels { get; set; } = [];

    public bool Automerge { get; set; }

    public IList<string> AutomergeLabels { get; set; } = [];

    public bool Deploy { get; set; }

    public string DeployComment { get; set; } = string.Empty;

    public IList<string> DeployEnvironments { get; set; } = [];

    public IList<string> IgnoreRepositories { get; set; } = [];

    public TimeSpan PublishTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public string QueueName { get; set; } = string.Empty;

    public IDictionary<string, RegistryOptions> Registries { get; set; } = new Dictionary<string, RegistryOptions>();

    public IList<string> RerunFailedChecks { get; set; } = [];

    public int RerunFailedChecksAttempts { get; set; }

    public TrustedEntitiesOptions TrustedEntities { get; set; } = new();
}
