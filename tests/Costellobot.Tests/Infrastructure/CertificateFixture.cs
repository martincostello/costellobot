// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
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
        Action<List<Claim>>? configureClaims = null,
        string audience = "https://github.com/martincostello",
        string issuer = "https://token.actions.githubusercontent.com",
        DateTime? notBefore = null,
        DateTime? expiresAt = null,
        DateTime? issuedAt = null)
    {
        var key = GetSecurityKey();
        var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        return CreateTokenCore(
            signingCredentials,
            repository,
            repositoryOwner,
            reference,
            subject,
            eventName,
            workflow,
            workflowReference,
            configureClaims,
            audience,
            issuer,
            notBefore,
            expiresAt,
            issuedAt);
    }

    public static string CreateUnsignedToken(
        string repository = "martincostello/example-repo",
        string repositoryOwner = "martincostello",
        string reference = "refs/heads/main",
        string? subject = null,
        string? eventName = null,
        string? workflow = null,
        string? workflowReference = null,
        Action<List<Claim>>? configureClaims = null,
        string audience = "https://github.com/martincostello",
        string issuer = "https://token.actions.githubusercontent.com",
        DateTime? notBefore = null,
        DateTime? expiresAt = null,
        DateTime? issuedAt = null)
        => CreateTokenCore(
            signingCredentials: null,
            repository,
            repositoryOwner,
            reference,
            subject,
            eventName,
            workflow,
            workflowReference,
            configureClaims,
            audience,
            issuer,
            notBefore,
            expiresAt,
            issuedAt);

    public static RsaSecurityKey GetSecurityKey()
    {
        var certificate = GetSharedSigningCertificate();
        var privateKey = certificate.GetRSAPrivateKey();

        return new(privateKey)
        {
            KeyId = _keyId,
        };
    }

    private static string CreateTokenCore(
        SigningCredentials? signingCredentials,
        string repository,
        string repositoryOwner,
        string reference,
        string? subject,
        string? eventName,
        string? workflow,
        string? workflowReference,
        Action<List<Claim>>? configureClaims,
        string audience,
        string issuer,
        DateTime? notBefore,
        DateTime? expiresAt,
        DateTime? issuedAt)
    {
        var utcNow = TimeProvider.System.GetUtcNow().UtcDateTime;
        var tokenNotBefore = notBefore ?? utcNow.AddMinutes(-1);
        var tokenExpiresAt = expiresAt ?? utcNow.AddMinutes(1);
        var tokenIssuedAt = issuedAt ?? utcNow;

        workflow ??= "build";

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Sub, subject ?? $"repo:{repository}:ref:{reference}"),
            new("event_name", eventName ?? "push"),
            new("ref", reference),
            new("ref_type", "branch"),
            new("repository", repository),
            new("repository_owner", repositoryOwner),
            new("workflow", workflow),
            new("workflow_ref", workflowReference ?? $"{repository}/.github/workflows/{workflow}@{reference}")
        ];

        configureClaims?.Invoke(claims);

        if (signingCredentials is null)
        {
            Dictionary<string, object> header = new()
            {
                [JwtHeaderParameterNames.Alg] = SecurityAlgorithms.None,
                [JwtHeaderParameterNames.Typ] = "JWT",
            };

            Dictionary<string, object> payload = new()
            {
                [JwtRegisteredClaimNames.Aud] = audience,
                [JwtRegisteredClaimNames.Exp] = new DateTimeOffset(tokenExpiresAt).ToUnixTimeSeconds(),
                [JwtRegisteredClaimNames.Iat] = new DateTimeOffset(tokenIssuedAt).ToUnixTimeSeconds(),
                [JwtRegisteredClaimNames.Iss] = issuer,
                [JwtRegisteredClaimNames.Nbf] = new DateTimeOffset(tokenNotBefore).ToUnixTimeSeconds(),
            };

            foreach (var claim in claims)
            {
                payload[claim.Type] = claim.Value;
            }

            return $"{EncodeTokenSegment(header)}.{EncodeTokenSegment(payload)}.";
        }

        var identity = new ClaimsIdentity(claims);

        var tokenDescriptor = new SecurityTokenDescriptor()
        {
            Audience = audience,
            Expires = tokenExpiresAt,
            IssuedAt = tokenIssuedAt,
            Issuer = issuer,
            NotBefore = tokenNotBefore,
            SigningCredentials = signingCredentials,
            Subject = identity,
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(tokenDescriptor);
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

    private static string EncodeTokenSegment<T>(T value)
        => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));
}
