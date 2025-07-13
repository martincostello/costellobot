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

    public override HttpClient CreateHttpClientForApp()
    {
        var handler = new HttpClientHandler()
        {
            CheckCertificateRevocationList = true,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };

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
