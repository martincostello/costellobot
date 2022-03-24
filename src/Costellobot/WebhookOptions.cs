// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Costellobot;

public sealed class WebhookOptions
{
    public bool Approve { get; set; }

    public bool Automerge { get; set; }

    public bool Comment { get; set; }

    public int RerunFailedChecksAttempts { get; set; }

    public TrustedEntitiesOptions TrustedEntities { get; set; } = new();
}
