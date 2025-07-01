// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Google.Apis.Http;

namespace MartinCostello.Costellobot;

public sealed class GoogleHttpClientFactoryAdapter(IHttpMessageHandlerFactory handlerFactory) : HttpClientFactory
{
    protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args)
        => handlerFactory.CreateHandler();
}
