// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace MartinCostello.Costellobot.Benchmarks;

[EventPipeProfiler(EventPipeProfile.CpuSampling)]
[MemoryDiagnoser]
public class AppBenchmarks : IAsyncDisposable
{
    private AppServer? _app = new();
    private HttpClient? _client;
    private bool _disposed;

    [GlobalSetup]
    public async Task StartServer()
    {
        if (_app is { } app)
        {
            await app.StartAsync();
            _client = app.CreateHttpClient();
        }
    }

    [GlobalCleanup]
    public async Task StopServer()
    {
        if (_app is { } app)
        {
            await app.StopAsync();
            _app = null;
        }
    }

    [Benchmark]
    public async Task<byte[]> Root()
        => await _client!.GetByteArrayAsync("/");

    [Benchmark]
    public async Task<byte[]> Version()
        => await _client!.GetByteArrayAsync("/version");

    [Benchmark]
    public async Task<byte[]> Webhook()
    {
        var payload = new
        {
            zen = "You tried your best and you failed miserably. The lesson is, never try.",
            hook_id = 109948940,
            hook = new
            {
                type = "App",
                id = 109948940,
                name = "web",
                active = true,
                events = new[] { "*" },
            },
            config = new
            {
                content_type = "json",
                insecure_ssl = "0",
                url = "https://costellobot.martincostello.local/github-webhook",
            },
            updated_at = "2022-03-23T23:13:43Z",
            created_at = "2022-03-23T23:13:43Z",
            app_id = 349596565,
            deliveries_url = "https://api.github.com/app/hook/deliveries",
            installation = new
            {
                id = 42,
            },
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/github-webhook");
        message.Headers.Add("X-GitHub-Delivery", "0a9af448-2c9e-4fca-bcc5-4af1d1149dbc");
        message.Headers.Add("X-GitHub-Event", "ping");
        message.Headers.Add("X-Hub-Signature-256", "sha256=b495b976c00ff7450a4715fd0eb812ce258643990116a925688d209b1f46e353");
        message.Content = JsonContent.Create(payload);

        using var response = await _client!.SendAsync(message);

        response.EnsureSuccessStatusCode();

        return await response!.Content!.ReadAsByteArrayAsync();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (!_disposed)
        {
            _client?.Dispose();
            _client = null;

            if (_app is not null)
            {
                await _app.DisposeAsync();
                _app = null;
            }
        }

        _disposed = true;
    }
}
