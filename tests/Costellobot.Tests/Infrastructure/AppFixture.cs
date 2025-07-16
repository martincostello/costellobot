// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AspNet.Security.OAuth.GitHub;
using Azure.Messaging.ServiceBus;
using JustEat.HttpClientInterception;
using MartinCostello.Logging.XUnit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MartinCostello.Costellobot.Infrastructure;

public class AppFixture : WebApplicationFactory<Program>, ITestOutputHelperAccessor
{
    private readonly Dictionary<string, string?> _configOverrides = new(StringComparer.OrdinalIgnoreCase);

    private DateTimeOffset? _utcNow;

    public AppFixture()
    {
        ClientOptions.AllowAutoRedirect = false;
        ClientOptions.BaseAddress = new Uri("https://localhost");
        Interceptor = new HttpClientInterceptorOptions().ThrowsOnMissingRegistration();
    }

    public HttpClientInterceptorOptions Interceptor { get; }

    public ITestOutputHelper? OutputHelper { get; set; }

    public virtual Uri ServerUri => ClientOptions.BaseAddress;

    public void ClearOutputHelper()
        => OutputHelper = null;

    public void SetOutputHelper(ITestOutputHelper value)
        => OutputHelper = value;

    public void ClearConfigurationOverrides(bool reload = true)
    {
        _configOverrides.Clear();

        if (reload)
        {
            ReloadConfiguration();
        }
    }

    public async Task ClearCacheAsync() =>
        await Services.GetRequiredService<HybridCache>().RemoveByTagAsync("all");

    public virtual HttpClient CreateHttpClientForApp() => CreateDefaultClient();

    public void OverrideConfiguration(string key, string value, bool reload = true)
    {
        _configOverrides[key] = value;

        if (reload)
        {
            ReloadConfiguration();
        }
    }

    public async Task<AntiforgeryTokens> GetAntiforgeryTokensAsync(
        Func<HttpClient>? httpClientFactory = null,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = httpClientFactory?.Invoke() ?? CreateClient();

        var tokens = await httpClient.GetFromJsonAsync<AntiforgeryTokens>(
            AntiforgeryTokenController.GetTokensUri,
            cancellationToken);

        return tokens!;
    }

    public void ChangeClock(DateTimeOffset utcNow)
        => _utcNow = utcNow;

    public void UseSystemClock()
        => _utcNow = null;

    protected override void ConfigureClient(HttpClient client)
        => client.BaseAddress = ServerUri;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:AzureServiceBus",
            "costellobot.servicebus.windows.local");

        builder.UseSetting(
            "ConnectionStrings:AzureTableStorage",
            "UseDevelopmentStorage=true");

        builder.UseSetting("Aspire:Azure:Data:Tables:DisableHealthChecks", "true");
        builder.UseSetting("Aspire:Azure:Messaging:ServiceBus:HealthCheckQueueName", string.Empty);
        builder.UseSetting("Aspire:Azure:Storage:Blobs:DisableHealthChecks", "true");

        builder.ConfigureAppConfiguration((configBuilder) =>
        {
            string testKey = File.ReadAllText("costellobot-tests.pem");

            var config = new[]
            {
                KeyValuePair.Create<string, string?>("WEBSITE_AUTH_ENCRYPTION_KEY", "website-auth-encryption-key"),
                KeyValuePair.Create<string, string?>("ConnectionStrings:AzureBlobStorage", string.Empty),
                KeyValuePair.Create<string, string?>("ConnectionStrings:AzureKeyVault", string.Empty),
                KeyValuePair.Create<string, string?>("GitHub:AccessToken", "gho_github-access-token"),
                KeyValuePair.Create<string, string?>($"GitHub:Apps:{Builders.GitHubFixtures.AppId}:ClientId", "github-app-client-id"),
                KeyValuePair.Create<string, string?>($"GitHub:Apps:{Builders.GitHubFixtures.AppId}:PrivateKey", testKey),
                KeyValuePair.Create<string, string?>("GitHub:BadgesKey", "badges-key"),
                KeyValuePair.Create<string, string?>("GitHub:ClientId", "github-id"),
                KeyValuePair.Create<string, string?>("GitHub:ClientSecret", "github-secret"),
                KeyValuePair.Create<string, string?>("GitHub:EnterpriseDomain", string.Empty),
                KeyValuePair.Create<string, string?>("GitHub:OAuthId", "github-oauth"),
                KeyValuePair.Create<string, string?>("GitHub:WebhookSecret", "github-webhook-secret"),
                KeyValuePair.Create<string, string?>("Google:CalendarIds:0", "google-calendar-id"),
                KeyValuePair.Create<string, string?>("Google:ClientEmail", "costellobot@google.local"),
                KeyValuePair.Create<string, string?>("Google:PrivateKey", CreateSigningCertificate()),
                KeyValuePair.Create<string, string?>("Google:PrivateKeyId", "github-app-client-id"),
                KeyValuePair.Create<string, string?>("Google:ProjectId", "google-project-id"),
                KeyValuePair.Create<string, string?>("Grafana:Token", "grafana-token"),
                KeyValuePair.Create<string, string?>("Grafana:Url", "https://grafana.local"),
                KeyValuePair.Create<string, string?>("HostOptions:ShutdownTimeout", "00:00:01"),
                KeyValuePair.Create<string, string?>("Site:AdminUsers:0", "john-smith"),
                KeyValuePair.Create<string, string?>("Webhook:DeployEnvironments:0", "production"),
                KeyValuePair.Create<string, string?>("Webhook:IgnoreRepositories:0", "ignored-org/ignored-repo"),
                KeyValuePair.Create<string, string?>("Webhook:QueueName", "github-webhooks"),
            };

            configBuilder.AddInMemoryCollection(config);
            configBuilder.Add(new AppFixtureConfigurationSource(this));
        });

        builder.ConfigureAntiforgeryTokenResource();

        builder.ConfigureLogging(
            (loggingBuilder) =>
                loggingBuilder.ClearProviders()
                              .AddXUnit(this)
                              .AddSignalR()
                              .AddFakeLogging()
                              .AddFilter("MartinCostello.Costellobot", (_) => true));

        builder.UseEnvironment(Environments.Production);

        builder.UseSolutionRelativeContentRoot(Path.Join("src", "Costellobot"), "*.slnx");

        builder.ConfigureServices((services) =>
        {
            services.AddHttpClient();

            services.AddSingleton<TimeProvider>((_) => new AppFixtureTimeProvider(this));

            services.AddSingleton<IHttpMessageHandlerBuilderFilter, HttpRequestInterceptionFilter>(
                (_) => new HttpRequestInterceptionFilter(Interceptor));

            services.AddSingleton<IPostConfigureOptions<GitHubAuthenticationOptions>, RemoteAuthorizationEventsFilter>();
            services.AddScoped<LoopbackOAuthEvents>();

            services.AddSingleton<ITrustStore, InMemoryTrustStore>();
            services.AddSingleton<ServiceBusClient, InMemoryServiceBusClient>();
        });

        Interceptor.RegisterOAuthBundle();
    }

    private static string? CreateSigningCertificate()
    {
        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddHours(1);

        using var key = RSA.Create();
        var request = new CertificateRequest("CN=google-cloud-certificate", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        using var certificate = request.CreateSelfSigned(notBefore, notAfter);
        return certificate.GetRSAPrivateKey()?.ExportPkcs8PrivateKeyPem();
    }

    private void ReloadConfiguration()
    {
        if (Services.GetRequiredService<IConfiguration>() is IConfigurationRoot root)
        {
            root.Reload();
        }
    }

    private sealed class AppFixtureConfigurationProvider : ConfigurationProvider
    {
        internal AppFixtureConfigurationProvider(AppFixture fixture)
        {
            Data = fixture._configOverrides;
        }
    }

    private sealed class AppFixtureConfigurationSource(AppFixture fixture) : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder)
            => new AppFixtureConfigurationProvider(fixture);
    }

    private sealed class AppFixtureTimeProvider(AppFixture fixture) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
            => fixture._utcNow ?? base.GetUtcNow();
    }
}
