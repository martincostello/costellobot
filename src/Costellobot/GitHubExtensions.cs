// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using MartinCostello.Costellobot.Handlers;
using MartinCostello.Costellobot.Registries;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Octokit;
using Octokit.Internal;
using Octokit.Webhooks;
using Octokit.Webhooks.AspNetCore;

namespace MartinCostello.Costellobot;

public static class GitHubExtensions
{
    private static readonly ProductHeaderValue UserAgent = CreateUserAgent();

    public static IServiceCollection AddGitHub(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddGitHubAuthentication(configuration);

        //// HACK Workaround for https://github.com/dotnet/extensions/issues/6297
        ////services.AddHttpClient()
        ////        .ConfigureHttpClientDefaults((p) => p.AddStandardResilienceHandler());
        services.AddHttpClient();

        services.AddHybridCache((p) => p.ReportTagMetrics = true);
        services.AddOptions();

        services.Configure<GitHubOptions>(configuration.GetSection("GitHub"));
        services.Configure<GitHubWebhookOptions>((p) => p.Secret = configuration["GitHub:WebhookSecret"]);
        services.Configure<SiteOptions>(configuration.GetSection("Site"));
        services.Configure<WebhookOptions>(configuration.GetSection("Webhook"));

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton((provider) =>
        {
            // Use a custom CryptoProviderFactory so that keys are not cached and then disposed of.
            // See https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/issues/1302.
            return new CryptoProviderFactory() { CacheSignatureProviders = false };
        });

        services.AddKeyedSingleton<UserCredentialStore>(string.Empty);

        services.AddSingleton<ICredentialStore>((provider) => provider.GetRequiredService<AppCredentialStore>());
        services.AddSingleton<IGitHubClientFactory, GitHubClientFactory>();
        services.AddSingleton<IJsonSerializer, SimpleJsonSerializer>();

        services.AddTransient<IHttpClient>((provider) =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();
            return new HttpClientAdapter(httpClientFactory.CreateHandler);
        });

        services.AddTransient<IGitHubClientForUser>((provider) => provider.CreateClient<UserCredentialStore>(string.Empty));

        var installations = GetInstallations(configuration);

        foreach ((var installationId, var appId) in installations)
        {
            services.AddKeyedSingleton(appId, (provider, key) =>
            {
                return new AppCredentialStore(
                    provider.GetRequiredService<HybridCache>(),
                    provider.GetRequiredService<CryptoProviderFactory>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<IOptionsMonitor<GitHubOptions>>())
                {
                    AppId = Convert.ToString(key, CultureInfo.InvariantCulture)!,
                };
            });

            services.AddKeyedSingleton(installationId, (provider, key) =>
            {
                return new InstallationCredentialStore(
                    provider.GetRequiredKeyedService<IGitHubClientForApp>(appId),
                    provider.GetRequiredService<HybridCache>())
                {
                    InstallationId = Convert.ToInt64(key, CultureInfo.InvariantCulture),
                };
            });

            services.AddKeyedTransient<IGitHubClientForApp>(appId, (provider, key) => provider.CreateClient<AppCredentialStore>(key));
            services.AddKeyedTransient<IGitHubClientForInstallation>(installationId, (provider, key) => provider.CreateClient<InstallationCredentialStore>(key));

            services.AddKeyedTransient<Octokit.GraphQL.IConnection>(installationId, (provider, key) =>
            {
                var productInformation = new Octokit.GraphQL.ProductHeaderValue(UserAgent.Name, UserAgent.Version);

                var baseAddress = GetGitHubGraphQLUri(provider);
                var credentialStore = provider.GetRequiredKeyedService<InstallationCredentialStore>(key);

                var httpClient = provider.GetRequiredService<HttpClient>();

                return new Octokit.GraphQL.Connection(productInformation, baseAddress, credentialStore, httpClient);
            });
        }

        services.TryAddSingleton<ITrustStore, AzureTableTrustStore>();

        services.AddSingleton<WebhookEventProcessor, GitHubEventProcessor>();
        services.AddSingleton<GitHubEventHandler>();
        services.AddSingleton<GitHubMessageProcessor>();
        services.AddSingleton<GitHubWebhookQueue>();
        services.AddSingleton<GitHubWebhookService>();

        services.AddTransient<GitCommitAnalyzer>();
        services.AddTransient<GitHubWebhookDispatcher>();

        services.AddTransient<PullRequestAnalyzer>();
        services.AddTransient<PullRequestApprover>();

        services.AddPackageRegistry<GitHubActionsPackageRegistry>();
        services.AddPackageRegistry<GitSubmodulePackageRegistry>();
        services.AddPackageRegistry<NpmPackageRegistry>("Npm");
        services.AddPackageRegistry<NuGetPackageRegistry>("NuGet");

        services.AddSingleton<BadgeService>();
        services.AddSingleton<PublicHolidayProvider>();

        services.AddScoped<GitHubWebhookContext>();
        services.AddScoped<IHandlerFactory, HandlerFactory>();
        services.AddTransient<CheckSuiteHandler>();
        services.AddTransient<DeploymentProtectionRuleHandler>();
        services.AddTransient<DeploymentStatusHandler>();
        services.AddTransient<IssueCommentHandler>();
        services.AddTransient<PullRequestHandler>();
        services.AddTransient<PullRequestReviewHandler>();
        services.AddTransient<PushHandler>();

        services.AddHostedService<GitHubWebhookService>();

        return services;
    }

    private static Dictionary<string, string> GetInstallations(IConfiguration configuration)
    {
        var installations = configuration.GetRequiredSection("GitHub:Installations").Get<Dictionary<string, GitHubInstallationOptions>>();

        var result = new Dictionary<string, string>();

        if (installations is not null)
        {
            foreach ((var installationId, var app) in installations)
            {
                result[installationId] = app.AppId;
            }
        }

        return result;
    }

    private static void AddPackageRegistry<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services, string? httpClientName = null)
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

    private static Connection CreateConnection<T>(this IServiceProvider provider, object? key)
        where T : ICredentialStore
    {
        var baseAddress = GetGitHubUri(provider);
        var credentialStore = provider.GetRequiredKeyedService<T>(key);
        var httpClient = provider.GetRequiredService<IHttpClient>();
        var serializer = provider.GetRequiredService<IJsonSerializer>();

        if (baseAddress != GitHubClient.GitHubApiUrl)
        {
            baseAddress = new Uri(baseAddress, "/api/v3/");
        }

        return new Connection(UserAgent, baseAddress, credentialStore, httpClient, serializer);
    }

    private static GitHubClientAdapter CreateClient<T>(this IServiceProvider provider, object? key)
        where T : ICredentialStore
    {
        var connection = provider.CreateConnection<T>(key);
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
