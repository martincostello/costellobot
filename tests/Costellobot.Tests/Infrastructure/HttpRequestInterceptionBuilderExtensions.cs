// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using JustEat.HttpClientInterception;
using MartinCostello.Costellobot.Builders;

namespace MartinCostello.Costellobot.Infrastructure;

public static class HttpRequestInterceptionBuilderExtensions
{
    public static HttpClientInterceptorOptions RegisterGoogleBundle(this HttpClientInterceptorOptions options) =>
        options.RegisterBundleFromResourceStream("google");

    public static async Task<HttpClientInterceptorOptions> RegisterNuGetBundleAsync(
        this HttpClientInterceptorOptions options,
        CancellationToken cancellationToken = default) =>
        await options.RegisterBundleFromResourceStreamAsync("nuget-search", cancellationToken: cancellationToken);

    public static HttpClientInterceptorOptions RegisterOAuthBundle(this HttpClientInterceptorOptions options) =>
        options.RegisterBundleFromResourceStream("oauth-http-bundle");

    public static HttpClientInterceptorOptions RegisterBundleFromResourceStream(
        this HttpClientInterceptorOptions options,
        string name,
        IEnumerable<KeyValuePair<string, string>>? templateValues = default)
    {
        using var stream = GetStream(name);
        return options.RegisterBundleFromStream(stream, templateValues);
    }

    public static async Task<HttpClientInterceptorOptions> RegisterBundleFromResourceStreamAsync(
        this HttpClientInterceptorOptions options,
        string name,
        IEnumerable<KeyValuePair<string, string>>? templateValues = default,
        CancellationToken cancellationToken = default)
    {
        using var stream = GetStream(name);
        return await options.RegisterBundleFromStreamAsync(stream, templateValues, cancellationToken);
    }

    public static HttpRequestInterceptionBuilder WithJsonContent<T>(this HttpRequestInterceptionBuilder builder, T response)
        where T : ResponseBuilder
    {
        return builder.WithContent(() => JsonSerializer.SerializeToUtf8Bytes(response.Build()));
    }

    public static HttpRequestInterceptionBuilder WithJsonContent<T>(this HttpRequestInterceptionBuilder builder, IEnumerable<T> response)
        where T : ResponseBuilder
    {
        return builder.WithContent(() => JsonSerializer.SerializeToUtf8Bytes(response.Build()));
    }

    private static Stream GetStream(string name)
    {
        name = $"MartinCostello.Costellobot.Bundles.{name}.json";

        var type = typeof(HttpRequestInterceptionBuilderExtensions);
        var assembly = type.Assembly;
        var stream = assembly.GetManifestResourceStream(name);

        return stream ?? throw new ArgumentException($"The resource '{name}' was not found.", nameof(name));
    }
}
