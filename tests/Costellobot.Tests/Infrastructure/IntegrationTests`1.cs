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
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };
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

    protected virtual TimeSpan ProcessingTimeout { get; } = TimeSpan.FromSeconds(0.5);

    protected virtual TimeSpan ResultTimeout { get; } = TimeSpan.FromSeconds(1);

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
        await Task.Delay(TimeSpan.FromSeconds(0.5));
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

        using var redirectHandler = new RedirectHandler(Fixture.ClientOptions.MaxAutomaticRedirections);

        using var anonymousCookieHandler = new CookieContainerHandler();
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

    protected async Task WaitForProcessingAsync()
        => await Task.Delay(ProcessingTimeout);

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

        string payload = JsonSerializer.Serialize(value, IndentedOptions);

        // See https://github.com/octokit/webhooks.net/blob/1e4110fc02b858c9f0f363ee46c9313cc06caef5/src/Octokit.Webhooks.AspNetCore/GitHubWebhookExtensions.cs#L109-L115
        var encoding = Encoding.UTF8;

        byte[] key = encoding.GetBytes(webhookSecret);
        byte[] data = encoding.GetBytes(payload);

        byte[] hash = HMACSHA256.HashData(key, data);
        string hashString = Convert.ToHexStringLower(hash);

        return (payload, $"sha256={hashString}");
    }
}
