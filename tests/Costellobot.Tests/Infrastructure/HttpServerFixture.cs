// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;

namespace MartinCostello.Costellobot.Infrastructure;

public sealed class HttpServerFixture : AppFixture
{
    public HttpServerFixture()
    {
        UseKestrel(
            (server) => server.Listen(
                IPAddress.Loopback, 0, (listener) => listener.UseHttps(
                    (https) => https.ServerCertificate = LoadDevelopmentCertificate())));
    }

    public string ServerAddress
    {
        get
        {
            StartServer();
            return ServerUri.ToString();
        }
    }

    public override Uri ServerUri
    {
        get
        {
            StartServer();
            return base.ServerUri;
        }
    }

    public override HttpClient CreateHttpClientForApp(params DelegatingHandler[] handlers)
    {
        HttpMessageHandler handler = new HttpClientHandler()
        {
            AllowAutoRedirect = ClientOptions.AllowAutoRedirect,
            CheckCertificateRevocationList = true,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };

        if (handlers.Length > 0)
        {
            for (var i = handlers.Length - 1; i > 0; i--)
            {
                handlers[i - 1].InnerHandler = handlers[i];
            }

            handlers[^1].InnerHandler = handler;
            handler = handlers[0];
        }

        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = ServerUri,
        };
    }

    private static X509Certificate2 LoadDevelopmentCertificate()
    {
        var metadata = typeof(HttpServerFixture).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToArray();

        var fileName = metadata.First((p) => p.Key is "DevCertificateFileName").Value!;
        var password = metadata.First((p) => p.Key is "DevCertificatePassword").Value;

        return X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(fileName), password);
    }
}
