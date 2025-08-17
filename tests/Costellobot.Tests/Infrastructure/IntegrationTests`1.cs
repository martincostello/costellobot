// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using MartinCostello.Costellobot.Drivers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Infrastructure;

[Category("Integration")]
public abstract class IntegrationTests<T> : IAsyncLifetime, IDisposable
    where T : AppFixture
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };
    private readonly IDisposable _scope;

    protected IntegrationTests(T fixture, ITestOutputHelper outputHelper)
    {
        Fixture = fixture;
        OutputHelper = outputHelper;
        Fixture.SetOutputHelper(OutputHelper);
        _scope = Fixture.Interceptor.BeginScope();
        Fixture.Interceptor.RegisterOAuthBundle();
    }

    ~IntegrationTests()
    {
        Dispose(false);
    }

    protected virtual CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    protected T Fixture { get; }

    protected ITestOutputHelper OutputHelper { get; }

    protected virtual TimeSpan ProcessingTimeout { get; } = Debugger.IsAttached ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(0.5);

    protected virtual TimeSpan ResultTimeout { get; } = Debugger.IsAttached ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(1);

    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
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
            .ForRequestHeader("Accept", "application/vnd.github.v3+json")
            .Responds()
            .WithStatus(StatusCodes.Status200OK);

        return ConfigureRateLimit(builder);
    }

    protected void AssertNoErrorsLogged()
    {
        var collector = Fixture.Services.GetFakeLogCollector();
        var snapshot = collector.GetSnapshot();

        snapshot.Where((p) => p.Level >= Microsoft.Extensions.Logging.LogLevel.Warning)
                .ShouldBeEmpty("One or more errors or warnings were logged.");
    }

    protected async Task AssertTaskNotRun(TaskCompletionSource source)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(0.5), CancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }

        source.Task.Status.ShouldBe(TaskStatus.WaitingForActivation);
    }

    protected async Task<HttpClient> CreateAuthenticatedClientAsync(bool setAntiforgeryTokenHeader = true)
    {
        AntiforgeryTokens anonymousTokens = await Fixture.GetAntiforgeryTokensAsync();

        using var redirectHandler = new RedirectHandler(Fixture.ClientOptions.MaxAutomaticRedirections);

        using var anonymousCookieHandler = new CookieContainerHandler();
        anonymousCookieHandler.Container.Add(
            Fixture.ServerUri,
            new Cookie(anonymousTokens.CookieName, anonymousTokens.CookieValue));

        using var anonymousClient = Fixture.CreateDefaultClient(redirectHandler, anonymousCookieHandler);
        anonymousClient.DefaultRequestHeaders.Add(anonymousTokens.HeaderName, anonymousTokens.RequestToken);

        var parameters = Array.Empty<KeyValuePair<string?, string?>>();
        using var content = new FormUrlEncodedContent(parameters);

        using var response = await anonymousClient.PostAsync("/sign-in", content, CancellationToken);
        response.IsSuccessStatusCode.ShouldBeTrue($"Sign in failed with HTTP {response.StatusCode}.");

        var authenticatedTokens = await Fixture.GetAntiforgeryTokensAsync(() => anonymousClient, CancellationToken);

        var authenticatedCookieHandler = new CookieContainerHandler(anonymousCookieHandler.Container);

        try
        {
            var authenticatedClient = Fixture.CreateDefaultClient(authenticatedCookieHandler);

            try
            {
                if (setAntiforgeryTokenHeader)
                {
                    authenticatedClient.DefaultRequestHeaders.Add(authenticatedTokens.HeaderName, authenticatedTokens.RequestToken);
                }

                return authenticatedClient;
            }
            catch (Exception)
            {
                authenticatedClient.Dispose();
                throw;
            }
        }
        catch (Exception)
        {
            authenticatedCookieHandler.Dispose();
            throw;
        }
    }

    protected void RegisterCollaborator(PullRequestDriver driver, string login, bool isCollaborator)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{driver.Repository.FullName}/collaborators/{login}")
            .Responds()
            .WithStatus(isCollaborator ? HttpStatusCode.NoContent : HttpStatusCode.NotFound)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterCommitAndDiff(PullRequestDriver driver)
        => RegisterCommitAndDiff(driver.PullRequest, driver.Commit);

    protected void RegisterCommitAndDiff(PullRequestBuilder builder, GitHubCommitBuilder? commit = null)
    {
        commit ??= builder.CreateCommit();

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{builder.Repository.FullName}/commits/{commit.Sha}")
            .Responds()
            .WithJsonContent(commit)
            .RegisterWith(Fixture.Interceptor);

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{builder.Repository.FullName}/commits/{commit.Sha}/pulls")
            .Responds()
            .WithJsonContent(builder)
            .RegisterWith(Fixture.Interceptor);

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{builder.Repository.FullName}/pulls/{builder.Number}")
            .ForRequestHeader("Accept", "application/vnd.github.v3.diff")
            .Responds()
            .WithContentHeader("Content-Type", "application/vnd.github.diff; charset=utf-8")
            .WithContent(() => Encoding.UTF8.GetBytes(builder.Diff))
            .RegisterWith(Fixture.Interceptor);

        RegisterGetPullRequest(builder);
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

    protected void RegisterDependabotConfiguration(RepositoryBuilder repository, string reference)
    {
        string configuration =
            """
            version: 2
            updates:
            - package-ecosystem: "github-actions"
              directory: "/"
              schedule:
                interval: daily
                time: "12:00"
                timezone: Europe/London
            """;

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.FullName}/contents/.github/dependabot.yml")
            .ForQuery($"ref={reference}")
            .ForRequestHeader("Accept", "application/vnd.github.v3.raw")
            .Responds()
            .WithContent(configuration)
            .WithContentHeader("Content-Type", "application/vnd.github.v3.raw")
            .RegisterWith(Fixture.Interceptor);
    }

    protected TaskCompletionSource RegisterEnableAutomerge(
        PullRequestDriver driver,
        Action<HttpRequestInterceptionBuilder, TaskCompletionSource>? configure = null)
    {
        var data = new
        {
            enablePullRequestAutoMerge = new
            {
                number = new
                {
                    number = driver.PullRequest.Number,
                },
            },
        };

        return RegisterGraphQLQuery(
            (query) => query.Contains($@"pullRequestId:""{driver.PullRequest.NodeId}""", StringComparison.Ordinal),
            data,
            configure);
    }

    protected void RegisterGetPullRequest(PullRequestBuilder builder)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{builder.Repository.FullName}/pulls/{builder.Number}")
            .Responds()
            .WithJsonContent(builder)
            .RegisterWith(Fixture.Interceptor);
    }

    protected TaskCompletionSource RegisterMergePullRequest(PullRequestDriver driver, bool mergeable = true)
    {
        var pullRequestMerged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        CreateDefaultBuilder()
            .Requests()
            .ForPut()
            .ForPath($"/repos/{driver.PullRequest.Repository.FullName}/pulls/{driver.PullRequest.Number}/merge")
            .Responds()
            .WithStatus(mergeable ? StatusCodes.Status200OK : StatusCodes.Status405MethodNotAllowed)
            .WithSystemTextJsonContent(new { merged = mergeable })
            .WithInterceptionCallback((_) => pullRequestMerged.SetResult())
            .RegisterWith(Fixture.Interceptor);

        return pullRequestMerged;
    }

    protected TaskCompletionSource RegisterReview(PullRequestDriver driver)
    {
        var pullRequestApproved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        RegisterReview(
            driver,
            (p) => p.WithInterceptionCallback((_) => pullRequestApproved.SetResult()));

        return pullRequestApproved;
    }

    protected async Task RegisterNuGetHttpBundleAsync() =>
        await Fixture.Interceptor.RegisterNuGetBundleAsync(CancellationToken);

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
        client.DefaultRequestHeaders.Add("X-GitHub-Hook-Installation-Target-ID", AppId);
        client.DefaultRequestHeaders.Add("X-GitHub-Hook-Installation-Target-Type", "integration");
        client.DefaultRequestHeaders.Add("X-Hub-Signature", signature);
        client.DefaultRequestHeaders.Add("X-Hub-Signature-256", signature);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Act
        return await client.PostAsync("/github-webhook", content);
    }

    protected async Task WaitForProcessingAsync()
        => await Task.Delay(ProcessingTimeout);

    protected virtual void Dispose(bool disposing)
    {
        _scope?.Dispose();
        Fixture.ClearConfigurationOverrides();
        Fixture.Services.GetFakeLogCollector().GetSnapshot(clear: true);
    }

    protected virtual ValueTask DisposeAsync(bool disposing)
    {
        Dispose(true);
        return ValueTask.CompletedTask;
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

        string payload = JsonSerializer.Serialize(value, IndentedOptions);

        // See https://github.com/octokit/webhooks.net/blob/1e4110fc02b858c9f0f363ee46c9313cc06caef5/src/Octokit.Webhooks.AspNetCore/GitHubWebhookExtensions.cs#L109-L115
        var encoding = Encoding.UTF8;

        byte[] key = encoding.GetBytes(webhookSecret);
        byte[] data = encoding.GetBytes(payload);

        byte[] hash = HMACSHA256.HashData(key, data);
        string hashString = Convert.ToHexStringLower(hash);

        return (payload, $"sha256={hashString}");
    }

    private TaskCompletionSource RegisterGraphQLQuery(
        Predicate<string> queryPredicate,
        object data,
        Action<HttpRequestInterceptionBuilder, TaskCompletionSource>? configure = null)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var response = new { data };

        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath("graphql")
            .ForRequestHeader("Accept", "application/vnd.github.antiope-preview+json")
            .ForContent(async (request) =>
            {
                request.ShouldNotBeNull();

                byte[] body = await request.ReadAsByteArrayAsync();
                using var document = JsonDocument.Parse(body);

                var query = document.RootElement.GetProperty("query").GetString();

                query.ShouldNotBeNull();

                return queryPredicate(query);
            })
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(response)
            .WithInterceptionCallback((_) => tcs.SetResult());

        configure?.Invoke(builder, tcs);

        builder.RegisterWith(Fixture.Interceptor);

        return tcs;
    }

    private void RegisterReview(
        PullRequestDriver driver,
        Action<HttpRequestInterceptionBuilder> configure)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{driver.PullRequest.Repository.FullName}/pulls/{driver.PullRequest.Number}/reviews")
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(new { });

        configure(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }
}
