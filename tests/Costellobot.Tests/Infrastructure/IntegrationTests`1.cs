// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Infrastructure;

public abstract class IntegrationTests<T> : IAsyncLifetime, IDisposable
    where T : AppFixture
{
    private readonly IDisposable _scope;

    protected IntegrationTests(T fixture, ITestOutputHelper outputHelper)
    {
        Fixture = fixture;
        OutputHelper = outputHelper;
        Fixture.SetOutputHelper(OutputHelper);
        _scope = Fixture.Interceptor.BeginScope();

        // TODO Fix scope disposal removing the existing bundle
        Fixture.Interceptor.RegisterBundle(Path.Combine("Bundles", "oauth-http-bundle.json"));
    }

    ~IntegrationTests()
    {
        Dispose(false);
    }

    protected T Fixture { get; }

    protected ITestOutputHelper OutputHelper { get; }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected static async Task AssertTaskNotRun(TaskCompletionSource source)
    {
        await Task.Delay(TimeSpan.FromSeconds(0.2));
        source.Task.Status.ShouldBe(TaskStatus.WaitingForActivation);
    }

    protected static HttpRequestInterceptionBuilder ConfigureRateLimit(HttpRequestInterceptionBuilder builder)
    {
        string oneHourFromNowEpoch = DateTimeOffset.UtcNow
            .AddHours(1)
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        return builder
            .WithResponseHeader("x-ratelimit-limit", "5000")
            .WithResponseHeader("x-ratelimit-remaining", "4999")
            .WithResponseHeader("x-ratelimit-reset", oneHourFromNowEpoch);
    }

    protected static HttpRequestInterceptionBuilder CreateDefaultBuilder()
    {
        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForHttps()
            .ForHost("api.github.com")
            .ForGet()
            .Responds()
            .WithStatus(StatusCodes.Status200OK);

        return ConfigureRateLimit(builder);
    }

    protected async Task<HttpClient> CreateAuthenticatedClientAsync(bool setAntiforgeryTokenHeader = true)
    {
        AntiforgeryTokens anonymousTokens = await Fixture.GetAntiforgeryTokensAsync();

        var redirectHandler = new RedirectHandler(Fixture.ClientOptions.MaxAutomaticRedirections);

        var anonymousCookieHandler = new CookieContainerHandler();
        anonymousCookieHandler.Container.Add(
            Fixture.Server.BaseAddress,
            new Cookie(anonymousTokens.CookieName, anonymousTokens.CookieValue));

        using var anonymousClient = Fixture.CreateDefaultClient(redirectHandler, anonymousCookieHandler);
        anonymousClient.DefaultRequestHeaders.Add(anonymousTokens.HeaderName, anonymousTokens.RequestToken);

        var parameters = Array.Empty<KeyValuePair<string?, string?>>();
        using var content = new FormUrlEncodedContent(parameters);

        using var response = await anonymousClient.PostAsync("/sign-in", content);
        response.IsSuccessStatusCode.ShouldBeTrue();

        var authenticatedTokens = await Fixture.GetAntiforgeryTokensAsync(() => anonymousClient);

        var authenticatedCookieHandler = new CookieContainerHandler(anonymousCookieHandler.Container);

        var authenticatedClient = Fixture.CreateDefaultClient(authenticatedCookieHandler);

        if (setAntiforgeryTokenHeader)
        {
            authenticatedClient.DefaultRequestHeaders.Add(authenticatedTokens.HeaderName, authenticatedTokens.RequestToken);
        }

        return authenticatedClient;
    }

    protected void RegisterEnableAutomerge(
        PullRequestBuilder pullRequest,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        var response = new
        {
            data = new
            {
                enablePullRequestAutoMerge = new
                {
                    number = new
                    {
                        number = pullRequest.Number,
                    },
                },
            },
        };

        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath("graphql")
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(response);

        configure?.Invoke(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetAccessToken(AccessTokenBuilder? accessToken = null)
    {
        accessToken ??= new();

        CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/app/installations/{InstallationId}/access_tokens")
            .Responds()
            .WithJsonContent(accessToken)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetCommit(GitHubCommitBuilder commit)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{commit.Repository.Owner.Login}/{commit.Repository.Name}/commits/{commit.Sha}")
            .Responds()
            .WithJsonContent(commit)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetCompare(
        GitHubCommitBuilder @base,
        GitHubCommitBuilder head,
        CompareResultBuilder comparison)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{@base.Repository.Owner.Login}/{@base.Repository.Name}/compare/{@base.Sha}...{head.Sha}")
            .Responds()
            .WithJsonContent(comparison)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetDeployments(
        RepositoryBuilder repository,
        string environmentName,
        params DeploymentBuilder[] deployments)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}/deployments")
            .ForQuery($"environment={environmentName}")
            .Responds()
            .WithJsonContent(deployments)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetDeploymentStatuses(
        RepositoryBuilder repository,
        DeploymentBuilder deployment,
        params DeploymentStatusBuilder[] deploymentStatuses)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}/deployments/{deployment.Id}/statuses")
            .Responds()
            .WithJsonContent(deploymentStatuses)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetPendingDeployments(
        RepositoryBuilder repository,
        long runId,
        params PendingDeploymentBuilder[] deployments)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}/actions/runs/{runId}/pending_deployments")
            .Responds()
            .WithJsonContent(deployments)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterApprovePendingDeployments(
        RepositoryBuilder repository,
        long runId,
        DeploymentBuilder deployment,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}/actions/runs/{runId}/pending_deployments")
            .Responds()
            .WithJsonContent(deployment);

        configure?.Invoke(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetPullRequestsForCommit(
        RepositoryBuilder repository,
        string sha,
        params PullRequestBuilder[] pullRequests)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}/commits/{sha}/pulls")
            .Responds()
            .WithJsonContent(pullRequests)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetWorkflows(
        CheckSuiteBuilder checkSuite,
        params WorkflowRunBuilder[] workflows)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{checkSuite.Repository.Owner.Login}/{checkSuite.Repository.Name}/actions/runs")
            .ForQuery($"check_suite_id={checkSuite.Id}")
            .Responds()
            .WithJsonContent(CreateWorkflowRuns(workflows))
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterPostReview(
        PullRequestBuilder pullRequest,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{pullRequest.Repository.Owner.Login}/{pullRequest.Repository.Name}/pulls/{pullRequest.Number}/reviews")
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(new { });

        configure?.Invoke(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    protected async Task<HttpResponseMessage> PostWebhookAsync(
        string @event,
        object value,
        string? webhookSecret = null,
        string? delivery = null)
    {
        (string payload, string signature) = CreateWebhook(value, webhookSecret);

        using var client = Fixture.CreateHttpClientForApp();

        client.DefaultRequestHeaders.Add("Accept", "*/*");
        client.DefaultRequestHeaders.Add("User-Agent", "GitHub-Hookshot/f05835d");
        client.DefaultRequestHeaders.Add("X-GitHub-Delivery", delivery ?? Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-GitHub-Event", @event);
        client.DefaultRequestHeaders.Add("X-GitHub-Hook-ID", "109948940");
        client.DefaultRequestHeaders.Add("X-GitHub-Hook-Installation-Target-ID", "42");
        client.DefaultRequestHeaders.Add("X-GitHub-Hook-Installation-Target-Type", "integration");
        client.DefaultRequestHeaders.Add("X-Hub-Signature", signature);
        client.DefaultRequestHeaders.Add("X-Hub-Signature-256", signature);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Act
        return await client.PostAsync("/github-webhook", content);
    }

    protected virtual void Dispose(bool disposing)
    {
        _scope?.Dispose();
        Fixture.ClearConfigurationOverrides();
    }

    private (string Payload, string Signature) CreateWebhook(
        object value,
        string? webhookSecret)
    {
        if (webhookSecret is null)
        {
            var options = Fixture.Services.GetRequiredService<IOptions<GitHubOptions>>().Value;
            webhookSecret = options.WebhookSecret;
        }

        string payload = JsonSerializer.Serialize(value, new JsonSerializerOptions() { WriteIndented = true });

        // See https://github.com/terrajobst/Terrajobst.GitHubEvents/blob/cb86100c783373e198cefb1ed7e92526a44833b0/src/Terrajobst.GitHubEvents.AspNetCore/GitHubEventsExtensions.cs#L112-L119
        var encoding = Encoding.UTF8;

        byte[] key = encoding.GetBytes(webhookSecret);
        byte[] data = encoding.GetBytes(payload);

        byte[] hash = HMACSHA256.HashData(key, data);
        string hashString = Convert.ToHexString(hash).ToLowerInvariant();

        return (payload, $"sha256={hashString}");
    }
}
