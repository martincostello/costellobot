// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Costellobot.Handlers;
using MartinCostello.Costellobot.Registries;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Octokit;
using Octokit.Internal;
using Octokit.Webhooks;

namespace MartinCostello.Costellobot;

public static class GitHubExtensions
{
    private static readonly ProductHeaderValue UserAgent = CreateUserAgent();

    public static IServiceCollection AddGitHub(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddGitHubAuthentication(configuration, environment);

        services.AddHttpClient()
                .ConfigureHttpClientDefaults((p) => p.AddStandardResilienceHandler());

        services.AddMemoryCache();
        services.AddOptions();

        services.Configure<GitHubOptions>(configuration.GetSection("GitHub"));
        services.Configure<SiteOptions>(configuration.GetSection("Site"));
        services.Configure<WebhookOptions>(configuration.GetSection("Webhook"));

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton((provider) =>
        {
            // Use a custom CryptoProviderFactory so that keys are not cached and then disposed of.
            // See https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/issues/1302.
            return new CryptoProviderFactory() { CacheSignatureProviders = false };
        });

        services.AddSingleton<AppCredentialStore>();
        services.AddSingleton<InstallationCredentialStore>();
        services.AddSingleton<ICredentialStore>((provider) => provider.GetRequiredService<AppCredentialStore>());
        services.AddSingleton<IJsonSerializer, SimpleJsonSerializer>();

        services.AddTransient<IHttpClient>((provider) =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();
            return new HttpClientAdapter(httpClientFactory.CreateHandler);
        });

        services.AddTransient<IGitHubClientForApp>((provider) => provider.CreateClient<AppCredentialStore>());
        services.AddTransient<IGitHubClientForInstallation>((provider) => provider.CreateClient<InstallationCredentialStore>());

        services.AddTransient<Octokit.GraphQL.IConnection>((provider) =>
        {
            var productInformation = new Octokit.GraphQL.ProductHeaderValue(UserAgent.Name, UserAgent.Version);

            var baseAddress = GetGitHubGraphQLUri(provider);
            var credentialStore = provider.GetRequiredService<InstallationCredentialStore>();
            var httpClient = provider.GetRequiredService<HttpClient>();

            return new Octokit.GraphQL.Connection(productInformation, baseAddress, credentialStore, httpClient);
        });

        services.AddSingleton<WebhookEventProcessor, GitHubEventProcessor>();
        services.AddSingleton<GitHubWebhookQueue>();
        services.AddSingleton<GitHubWebhookService>();
        services.AddTransient<GitCommitAnalyzer>();
        services.AddTransient<GitHubWebhookDispatcher>();

        services.AddPackageRegistry<GitHubActionsPackageRegistry>();
        services.AddPackageRegistry<GitSubmodulePackageRegistry>();
        services.AddPackageRegistry<NpmPackageRegistry>("Npm");
        services.AddPackageRegistry<NuGetPackageRegistry>("NuGet");

        services.AddSingleton<PublicHolidayProvider>();

        services.AddSingleton<IHandlerFactory, HandlerFactory>();
        services.AddTransient<CheckSuiteHandler>();
        services.AddTransient<DeploymentProtectionRuleHandler>();
        services.AddTransient<DeploymentStatusHandler>();
        services.AddTransient<IssueCommentHandler>();
        services.AddTransient<PullRequestHandler>();
        services.AddTransient<PushHandler>();

        services.AddHostedService<GitHubWebhookService>();

        return services;
    }

    private static void AddPackageRegistry<T>(this IServiceCollection services, string? httpClientName = null)
        where T : class, IPackageRegistry
    {
        if (httpClientName is { } name)
        {
            services.AddHttpClient<IPackageRegistry, T>(name, (provider, client) => ConfigureRegistry(name, provider, client));
        }
        else
        {
            services.AddScoped<IPackageRegistry, T>();
        }

        static void ConfigureRegistry(string name, IServiceProvider provider, HttpClient client)
        {
            var options = provider.GetRequiredService<IOptions<WebhookOptions>>().Value;
            if (options.Registries.TryGetValue(name, out var endpoint))
            {
                client.BaseAddress = endpoint.BaseAddress;
                client.Timeout = endpoint.Timeout;
            }
        }
    }

    private static Connection CreateConnection<T>(this IServiceProvider provider)
        where T : ICredentialStore
    {
        var baseAddress = GetGitHubUri(provider);
        var credentialStore = provider.GetRequiredService<T>();
        var httpClient = provider.GetRequiredService<IHttpClient>();
        var serializer = provider.GetRequiredService<IJsonSerializer>();

        if (baseAddress != GitHubClient.GitHubApiUrl)
        {
            baseAddress = new Uri(baseAddress, "/api/v3/");
        }

        return new Connection(UserAgent, baseAddress, credentialStore, httpClient, serializer);
    }

    private static GitHubClientAdapter CreateClient<T>(this IServiceProvider provider)
        where T : ICredentialStore
    {
        var connection = provider.CreateConnection<T>();
        return new GitHubClientAdapter(connection);
    }

    private static ProductHeaderValue CreateUserAgent()
    {
        string productVersion = typeof(GitHubExtensions).Assembly.GetName().Version!.ToString(3);
        return new ProductHeaderValue("Costellobot", productVersion);
    }

    private static Uri GetGitHubUri(IServiceProvider provider)
    {
        var baseAddress = GitHubClient.GitHubApiUrl;

        var configuration = provider.GetRequiredService<IConfiguration>();

        if (configuration["GitHub:EnterpriseDomain"] is string enterpriseDomain &&
            !string.IsNullOrWhiteSpace(enterpriseDomain))
        {
            baseAddress = new(enterpriseDomain, UriKind.Absolute);
        }

        return baseAddress;
    }

    private static Uri GetGitHubGraphQLUri(IServiceProvider provider)
    {
        var baseAddress = Octokit.GraphQL.Connection.GithubApiUri;

        var configuration = provider.GetRequiredService<IConfiguration>();

        if (configuration["GitHub:EnterpriseDomain"] is string enterpriseDomain &&
            !string.IsNullOrWhiteSpace(enterpriseDomain))
        {
            var enterpriseUri = new Uri(enterpriseDomain, UriKind.Absolute);
            baseAddress = new(enterpriseUri, "api" + baseAddress.AbsolutePath);
        }

        return baseAddress;
    }
}
