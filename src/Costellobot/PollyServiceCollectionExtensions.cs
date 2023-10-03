// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Polly;
using Polly.Extensions.Http;

namespace MartinCostello.Costellobot;

public static class PollyServiceCollectionExtensions
{
    internal const string ReadPolicyName = "ReadPolicy";
    internal const string WritePolicyName = "WritePolicy";

    public static IServiceCollection AddPolly(this IServiceCollection services)
    {
        return services.AddPolicyRegistry((_, registry) =>
        {
            TimeSpan[] sleepDurations =
            [
                TimeSpan.FromSeconds(0.1),
                TimeSpan.FromSeconds(0.2),
                TimeSpan.FromSeconds(0.5),
            ];

            var readPolicy = HttpPolicyExtensions.HandleTransientHttpError()
                .WaitAndRetryAsync(sleepDurations)
                .WithPolicyKey(ReadPolicyName);

            var writePolicy = Policy.NoOpAsync()
                .AsAsyncPolicy<HttpResponseMessage>()
                .WithPolicyKey("WritePolicy");

            var policies = new[]
            {
                readPolicy,
                writePolicy,
            };

            foreach (var policy in policies)
            {
                registry.Add(policy.PolicyKey, policy);
            }
        });
    }
}
