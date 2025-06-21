// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;

namespace MartinCostello.Costellobot.Infrastructure;

public sealed class HttpServerFixture : AppFixture
{
    public HttpServerFixture()
    {
        // HACK Ensure the Kestrel port will eventually be used
        ClientOptions.BaseAddress = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions().BaseAddress;
        UseKestrel(0);
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureKestrel(
            (serverOptions) => serverOptions.ConfigureHttpsDefaults(
                (httpsOptions) => httpsOptions.ServerCertificate = X509CertificateLoader.LoadPkcs12FromFile("localhost-dev.pfx", "Pa55w0rd!")));
    }
}
