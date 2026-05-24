// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MartinCostello.Costellobot.Infrastructure;

public static class CertificateFixture
{
    private static readonly string _keyId = Guid.NewGuid().ToString();
    private static X509Certificate2? _certificate;

    public static string CreateToken(
        string repository = "martincostello/example-repo",
        string repositoryOwner = "martincostello",
        string reference = "refs/heads/main",
        string? subject = null,
        string? eventName = null,
        string? workflow = null,
        string? workflowReference = null,
        Action<List<Claim>>? configureClaims = null)
    {
        var key = GetSecurityKey();

        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var utcNow = TimeProvider.System.GetUtcNow().UtcDateTime;
        var notBefore = utcNow.AddMinutes(-1);
        var expiresAt = utcNow.AddMinutes(1);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Sub, subject ?? $"repo:{repository}:ref:{reference}"),
            new("event_name", eventName ?? "push"),
            new("ref", reference),
            new("ref_type", "branch"),
            new("repository", repository),
            new("repository_owner", repositoryOwner),
            new("workflow", workflow ?? "build"),
            new("workflow_ref", workflowReference ?? $"{repository}/.github/workflows/{workflow}@{reference}")
        ];

        configureClaims?.Invoke(claims);

        var identity = new ClaimsIdentity(claims);

        var tokenDescriptor = new SecurityTokenDescriptor()
        {
            Audience = "https://github.com/martincostello",
            Expires = expiresAt,
            IssuedAt = utcNow,
            Issuer = "https://token.actions.githubusercontent.local",
            NotBefore = notBefore,
            SigningCredentials = credentials,
            Subject = identity,
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(tokenDescriptor);
    }

    public static RsaSecurityKey GetSecurityKey()
    {
        var certificate = GetSharedSigningCertificate();
        var privateKey = certificate.GetRSAPrivateKey();

        return new(privateKey)
        {
            KeyId = _keyId,
        };
    }

    private static X509Certificate2 CreateSigningCertificate()
    {
        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddHours(1);

        using var key = RSA.Create();
        var request = new CertificateRequest("CN=costellobot.local", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static X509Certificate2 GetSharedSigningCertificate()
    {
        _certificate ??= CreateSigningCertificate();

        var rawData = _certificate.Export(X509ContentType.Pfx);
        return X509CertificateLoader.LoadPkcs12(rawData, null);
    }
}
