using System.IdentityModel.Tokens.Jwt;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using JiraClone.Application.Auth;
using JiraClone.Infrastructure.Auth;
using Microsoft.IdentityModel.Tokens;

namespace JiraClone.Tests.Infrastructure.Auth;

public class OAuthTokenValidationTests
{
    [Fact]
    public async Task ValidateIdTokenAsync_NonceMismatch_ThrowsSecurityException()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = CreateSigningKey(rsa);
        using var httpClient = CreateHttpClient(CreateJwksJson(signingKey));
        var service = CreateService(httpClient);
        var idToken = CreateIdToken(signingKey, issuer: TestIssuer, audience: TestClientId, nonce: "expected-nonce", expiresAtUtc: DateTime.UtcNow.AddMinutes(10));

        await Assert.ThrowsAsync<SecurityException>(() => service.ValidateIdTokenAsync(idToken, "different-nonce"));
    }

    [Fact]
    public async Task ValidateIdTokenAsync_ExpiredToken_ThrowsSecurityTokenExpiredException()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = CreateSigningKey(rsa);
        using var httpClient = CreateHttpClient(CreateJwksJson(signingKey));
        var service = CreateService(httpClient);
        var idToken = CreateIdToken(signingKey, issuer: TestIssuer, audience: TestClientId, nonce: TestNonce, expiresAtUtc: DateTime.UtcNow.AddMinutes(-10));

        await Assert.ThrowsAsync<SecurityTokenExpiredException>(() => service.ValidateIdTokenAsync(idToken, TestNonce));
    }

    [Fact]
    public async Task ValidateIdTokenAsync_WrongIssuer_ThrowsSecurityTokenInvalidIssuerException()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = CreateSigningKey(rsa);
        using var httpClient = CreateHttpClient(CreateJwksJson(signingKey));
        var service = CreateService(httpClient);
        var idToken = CreateIdToken(signingKey, issuer: "https://issuer.example.com/other", audience: TestClientId, nonce: TestNonce, expiresAtUtc: DateTime.UtcNow.AddMinutes(10));

        await Assert.ThrowsAsync<SecurityTokenInvalidIssuerException>(() => service.ValidateIdTokenAsync(idToken, TestNonce));
    }

    [Fact]
    public async Task ValidateIdTokenAsync_ValidToken_ReturnsPrincipal()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = CreateSigningKey(rsa);
        using var httpClient = CreateHttpClient(CreateJwksJson(signingKey));
        var service = CreateService(httpClient);
        var idToken = CreateIdToken(signingKey, issuer: TestIssuer, audience: TestClientId, nonce: TestNonce, expiresAtUtc: DateTime.UtcNow.AddMinutes(10));

        var principal = await service.ValidateIdTokenAsync(idToken, TestNonce);

        Assert.Equal("oauth.user@example.com", principal.FindFirst("email")?.Value);
        Assert.Equal(TestNonce, principal.FindFirst("nonce")?.Value);
    }

    private static OAuthService CreateService(HttpClient httpClient) =>
        new(
            new OAuthOptions
            {
                Enabled = true,
                ProviderName = "Test IdP",
                AuthorizationEndpoint = "https://issuer.example.com/authorize",
                TokenEndpoint = "https://issuer.example.com/token",
                Issuer = TestIssuer,
                JwksUri = "https://issuer.example.com/keys",
                ClientId = TestClientId,
                RedirectUri = "http://localhost:8765/callback",
                Scopes = ["openid", "profile", "email"]
            },
            httpClient);

    private static HttpClient CreateHttpClient(string jwksJson) =>
        new(new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(jwksJson)
        }));

    private static RsaSecurityKey CreateSigningKey(RSA rsa) =>
        new(rsa)
        {
            KeyId = Guid.NewGuid().ToString("N")
        };

    private static string CreateJwksJson(RsaSecurityKey signingKey)
    {
        var parameters = signingKey.Rsa?.ExportParameters(false) ?? throw new InvalidOperationException("RSA parameters were unavailable.");
        var jwks = new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = signingKey.KeyId,
                    alg = SecurityAlgorithms.RsaSha256,
                    e = Base64UrlEncoder.Encode(parameters.Exponent),
                    n = Base64UrlEncoder.Encode(parameters.Modulus)
                }
            }
        };

        return JsonSerializer.Serialize(jwks);
    }

    private static string CreateIdToken(SecurityKey signingKey, string issuer, string audience, string nonce, DateTime expiresAtUtc)
    {
        var notBeforeUtc = expiresAtUtc > DateTime.UtcNow
            ? DateTime.UtcNow.AddMinutes(-1)
            : expiresAtUtc.AddMinutes(-10);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim("nonce", nonce),
                new Claim("email", "oauth.user@example.com"),
                new Claim("name", "OAuth User")
            ],
            notBefore: notBeforeUtc,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler { MapInboundClaims = false }.WriteToken(token);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private const string TestIssuer = "https://issuer.example.com/v2.0";
    private const string TestClientId = "test-client-id";
    private const string TestNonce = "nonce-123";
}
