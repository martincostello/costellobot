// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using Microsoft.AspNetCore.Http;
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

    protected void RegisterGetCheckRuns(
        RepositoryBuilder repository,
        int id,
        params CheckRunBuilder[] checkRuns)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}/check-suites/{id}/check-runs")
            .ForQuery("status=completed&filter=all")
            .Responds()
            .WithJsonContent(CreateCheckRuns(checkRuns))
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

    protected void RegisterGetPullRequest(PullRequestBuilder pullRequest)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{pullRequest.Repository.Owner.Login}/{pullRequest.Repository.Name}/pulls/{pullRequest.Number}")
            .Responds()
            .WithJsonContent(pullRequest)
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

    protected void RegisterRerequestCheckSuite(
        CheckSuiteBuilder checkSuite,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{checkSuite.Repository.Owner.Login}/{checkSuite.Repository.Name}/check-suites/{checkSuite.Id}/rerequest")
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(new { });

        configure?.Invoke(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterRerunFailedJobs(
        WorkflowRunBuilder workflowRun,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{workflowRun.Repository.Owner.Login}/{workflowRun.Repository.Name}/actions/runs/{workflowRun.Id}/rerun-failed-jobs")
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(new { });

        configure?.Invoke(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    protected async Task<HttpResponseMessage> PostWebhookAsync(
        string @event,
        object value,
        string? webhookSecret = null)
    {
        (string payload, string signature) = CreateWebhook(value, webhookSecret);

        using var client = Fixture.CreateDefaultClient();

        client.DefaultRequestHeaders.Add("Accept", "*/*");
        client.DefaultRequestHeaders.Add("User-Agent", "GitHub-Hookshot/f05835d");
        client.DefaultRequestHeaders.Add("X-GitHub-Delivery", Guid.NewGuid().ToString());
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
