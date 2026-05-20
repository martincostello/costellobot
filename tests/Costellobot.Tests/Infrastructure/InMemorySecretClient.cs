// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;

namespace MartinCostello.Costellobot.Infrastructure;

internal sealed class InMemorySecretClient : SecretClient
{
    public InMemorySecretClient()
        : base(new Uri("https://github.vault.azure.local/"), new FakeTokenCredential())
    {
    }

    public override Task<Response<KeyVaultSecret>> GetSecretAsync(
        string name,
        string? version = null,
        SecretContentType? outContentType = null,
        CancellationToken cancellationToken = default)
    {
        var secret = new KeyVaultSecret(name, "not-a-secret");
        return Task.FromResult(Response.FromValue(secret, new SecretResponse(200)));
    }

    public override Task<Response<KeyVaultSecret>> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default)
        => GetSecretAsync(name, version, null, cancellationToken);

    private sealed class FakeTokenCredential : Azure.Core.TokenCredential
    {
        public override AccessToken GetToken(Azure.Core.TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new("not-a-token", TimeProvider.System.GetUtcNow().AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(Azure.Core.TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(GetToken(requestContext, cancellationToken));
    }

    private sealed class SecretResponse(int status) : Response
    {
        public override int Status => status;

        public override string ReasonPhrase => "OK";

        public override Stream? ContentStream { get; set; } = Stream.Null;

        public override string ClientRequestId { get; set; } = Guid.NewGuid().ToString();

        public override void Dispose()
        {
            // No-op
        }

        protected override bool ContainsHeader(string name) => false;

        protected override IEnumerable<HttpHeader> EnumerateHeaders() => [];

        protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
        {
            value = null;
            return false;
        }

        protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
        {
            values = null;
            return false;
        }
    }
}
