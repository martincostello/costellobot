// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.Logging.XUnit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Infrastructure;

public class AppFixture : WebApplicationFactory<Program>, ITestOutputHelperAccessor
{
    private readonly Dictionary<string, string> _configOverrides = new(StringComparer.OrdinalIgnoreCase);

    public AppFixture()
    {
        ClientOptions.AllowAutoRedirect = false;
        ClientOptions.BaseAddress = new Uri("https://localhost");
        Interceptor = new HttpClientInterceptorOptions().ThrowsOnMissingRegistration();
    }

    public HttpClientInterceptorOptions Interceptor { get; }

    public ITestOutputHelper? OutputHelper { get; set; }

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

    public void ClearCache()
    {
        var cache = Services.GetRequiredService<IMemoryCache>();

        if (cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(percentage: 100);
        }
    }

    public void OverrideConfiguration(string key, string value, bool reload = true)
    {
        _configOverrides[key] = value;

        if (reload)
        {
            ReloadConfiguration();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((configBuilder) =>
        {
            string testKey = File.ReadAllText("costellobot-tests.pem");

            var config = new[]
            {
                KeyValuePair.Create("AzureKeyVault:ClientId", string.Empty),
                KeyValuePair.Create("AzureKeyVault:ClientSecret", string.Empty),
                KeyValuePair.Create("AzureKeyVault:TenantId", string.Empty),
                KeyValuePair.Create("AzureKeyVault:Uri", string.Empty),
                KeyValuePair.Create("GitHub:AppId", "github-app-id"),
                KeyValuePair.Create("GitHub:InstallationId", InstallationId),
                KeyValuePair.Create("GitHub:PrivateKey", testKey),
                KeyValuePair.Create("GitHub:WebhookSecret", "github-webhook-secret"),
            };

            configBuilder.AddInMemoryCollection(config);
            configBuilder.Add(new AppFixtureConfigurationSource(this));
        });

        builder.ConfigureLogging(
            (loggingBuilder) => loggingBuilder.ClearProviders().AddXUnit(this));

        builder.UseEnvironment(Environments.Production);

        builder.UseSolutionRelativeContentRoot(Path.Combine("src", "Costellobot"));

        builder.ConfigureServices((services) =>
        {
            services.AddHttpClient();

            services.AddSingleton<IHttpMessageHandlerBuilderFilter, HttpRequestInterceptionFilter>(
                (_) => new HttpRequestInterceptionFilter(Interceptor));
        });
    }

    private void ReloadConfiguration()
    {
        var config = Services.GetRequiredService<IConfiguration>();

        if (config is IConfigurationRoot root)
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

    private sealed class AppFixtureConfigurationSource : IConfigurationSource
    {
        internal AppFixtureConfigurationSource(AppFixture fixture)
        {
            Fixture = fixture;
        }

        internal AppFixture Fixture { get; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
            => new AppFixtureConfigurationProvider(Fixture);
    }
}
