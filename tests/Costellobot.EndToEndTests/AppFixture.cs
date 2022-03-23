// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net.Http.Headers;

namespace MartinCostello.Costellobot;

public class AppFixture
{
    private const string ApplicationUrl = "APPLICATION_URL";

    private readonly Uri? _serverAddress;

    public AppFixture()
    {
        string url = Environment.GetEnvironmentVariable(ApplicationUrl) ?? string.Empty;

        if (!Uri.TryCreate(url, UriKind.Absolute, out _serverAddress))
        {
            _serverAddress = null;
        }
    }

    public Uri ServerAddress
    {
        get
        {
            Skip.If(_serverAddress is null, $"The {ApplicationUrl} environment variable is not set or is not a valid absolute URI.");
            return _serverAddress!;
        }
    }

    private static string Version => Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? "0";

    public HttpClient CreateClient()
    {
        var client = new HttpClient()
        {
            BaseAddress = ServerAddress,
        };

        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(
                "Costellobot.EndToEndTests", Version));

        return client;
    }
}
