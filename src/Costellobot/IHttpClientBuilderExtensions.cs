// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Polly;
using static MartinCostello.Costellobot.PollyServiceCollectionExtensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class IHttpClientBuilderExtensions
{
    public static IHttpClientBuilder ApplyDefaultConfiguration(this IHttpClientBuilder builder)
    {
        return builder.AddPolicyHandlerFromRegistry((registry, request) =>
        {
            string policyName =
                request.Method == HttpMethod.Get ?
                ReadPolicyName :
                WritePolicyName;

            return registry.Get<IAsyncPolicy<HttpResponseMessage>>(policyName);
        });
    }
}
