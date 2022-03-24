// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;
using Microsoft.AspNetCore.Http;
using static MartinCostello.Costellobot.Builders.GitHubFixtures;

namespace MartinCostello.Costellobot.Infrastructure;

public abstract class IntegrationTests<T> : IAsyncLifetime
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

    protected T Fixture { get; }

    protected ITestOutputHelper OutputHelper { get; }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual Task DisposeAsync()
    {
        _scope?.Dispose();
        Fixture.ClearConfigurationOverrides();
        return Task.CompletedTask;
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

    protected void RegisterEmojis()
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath("/emojis")
            .Responds()
            .WithJsonContent(new
            {
                wave = "https://github.githubassets.com/images/icons/emoji/unicode/1f44b.png?v8",
                rocket = "https://github.githubassets.com/images/icons/emoji/unicode/1f680.png?v8",
            })
            .RegisterWith(Fixture.Interceptor);
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

    protected void RegisterIssueComment(
        PullRequestBuilder pullRequest,
        IssueCommentBuilder issueComment,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{pullRequest.Repository.Owner.Login}/{pullRequest.Repository.Name}/issues/{pullRequest.Number}/comments")
            .Responds()
            .WithJsonContent(issueComment);

        configure?.Invoke(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }
}
