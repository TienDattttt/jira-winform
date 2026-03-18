using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JiraClone.Application.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Infrastructure.Auth;

public sealed class OAuthService : IOAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OAuthOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(OAuthOptions options, HttpClient httpClient, ILogger<OAuthService>? logger = null)
    {
        _options = options;
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<OAuthService>.Instance;
    }

    public async Task<OAuthResult> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var redirectUri = new Uri(_options.RedirectUri, UriKind.Absolute);
        ValidateRedirectUri(redirectUri);

        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state = GenerateNonce();
        var authorizationUrl = BuildAuthorizationUrl(redirectUri, codeChallenge, state);

        using var listener = CreateListener(redirectUri);
        using var cancellationRegistration = cancellationToken.Register(() => SafeStop(listener));
        listener.Start();

        _logger.LogInformation("Starting OAuth authorization flow against {AuthorizationEndpoint}.", _options.AuthorizationEndpoint);
        OpenBrowser(authorizationUrl);

        var callback = await WaitForCallbackAsync(listener, redirectUri, state, cancellationToken);
        var code = callback.Request.QueryString["code"];
        if (string.IsNullOrWhiteSpace(code))
        {
            await WriteBrowserResponseAsync(callback.Response, HttpStatusCode.BadRequest, "Sign-in failed", "Authorization code was not returned by the identity provider.");
            throw new InvalidOperationException("The identity provider did not return an authorization code.");
        }

        OAuthTokenResponse tokenResponse;
        try
        {
            tokenResponse = await ExchangeCodeAsync(code, codeVerifier, cancellationToken);
            var result = BuildResult(tokenResponse);
            await WriteBrowserResponseAsync(callback.Response, HttpStatusCode.OK, "Sign-in complete", "You can now return to Jira Desktop and close this browser tab.");
            return result;
        }
        catch
        {
            if (callback.Response.OutputStream.CanWrite)
            {
                try
                {
                    await WriteBrowserResponseAsync(callback.Response, HttpStatusCode.InternalServerError, "Sign-in failed", "Jira Desktop could not complete the OAuth sign-in flow. You can close this tab and try again.");
                }
                catch
                {
                }
            }

            throw;
        }
        finally
        {
            SafeStop(listener);
        }
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("OAuth sign-in is not configured. Check the OAuth section in appsettings.json.");
        }

        ValidateHttpsUri(_options.AuthorizationEndpoint, "AuthorizationEndpoint");
        ValidateHttpsUri(_options.TokenEndpoint, "TokenEndpoint");
    }

    private static void ValidateHttpsUri(string uriValue, string settingName)
    {
        if (!Uri.TryCreate(uriValue, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"OAuth:{settingName} must be a valid HTTPS URL.");
        }
    }

    private static void ValidateRedirectUri(Uri redirectUri)
    {
        if (!string.Equals(redirectUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || !redirectUri.IsLoopback)
        {
            throw new InvalidOperationException("OAuth:RedirectUri must use http://localhost or another loopback address for desktop sign-in.");
        }
    }

    private string BuildAuthorizationUrl(Uri redirectUri, string codeChallenge, string state)
    {
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri.ToString(),
            ["scope"] = string.Join(' ', _options.Scopes.Where(scope => !string.IsNullOrWhiteSpace(scope)).Select(scope => scope.Trim())),
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state
        };

        var builder = new StringBuilder(_options.AuthorizationEndpoint);
        builder.Append(_options.AuthorizationEndpoint.Contains('?') ? '&' : '?');
        builder.Append(string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")));
        return builder.ToString();
    }

    private async Task<HttpListenerContext> WaitForCallbackAsync(HttpListener listener, Uri redirectUri, string expectedState, CancellationToken cancellationToken)
    {
        var expectedPath = NormalizePath(redirectUri.AbsolutePath);

        while (true)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception exception) when (cancellationToken.IsCancellationRequested && exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                throw new OperationCanceledException("SSO sign-in was canceled or timed out.", exception, cancellationToken);
            }

            var requestUrl = context.Request.Url;
            if (requestUrl is null)
            {
                await WriteBrowserResponseAsync(context.Response, HttpStatusCode.BadRequest, "Invalid callback", "The callback URL was invalid.");
                continue;
            }

            if (!string.Equals(NormalizePath(requestUrl.AbsolutePath), expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                await WriteBrowserResponseAsync(context.Response, HttpStatusCode.NotFound, "Invalid callback", "This callback path is not registered for Jira Desktop sign-in.");
                continue;
            }

            var error = context.Request.QueryString["error"];
            if (!string.IsNullOrWhiteSpace(error))
            {
                var description = context.Request.QueryString["error_description"] ?? error;
                await WriteBrowserResponseAsync(context.Response, HttpStatusCode.BadRequest, "Sign-in failed", WebUtility.HtmlEncode(description));
                throw new InvalidOperationException($"The identity provider returned an error: {description}");
            }

            var state = context.Request.QueryString["state"];
            if (!string.Equals(state, expectedState, StringComparison.Ordinal))
            {
                await WriteBrowserResponseAsync(context.Response, HttpStatusCode.BadRequest, "Invalid state", "The sign-in response could not be verified.");
                throw new InvalidOperationException("The OAuth state parameter did not match the original request.");
            }

            return context;
        }
    }

    private async Task<OAuthTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = _options.ClientId,
                ["redirect_uri"] = _options.RedirectUri,
                ["code_verifier"] = codeVerifier
            })
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = TryReadError(responseContent) ?? $"Token endpoint returned HTTP {(int)response.StatusCode}.";
            throw new InvalidOperationException(error);
        }

        var tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(responseContent, JsonOptions)
            ?? throw new InvalidOperationException("The token response could not be parsed.");
        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken) || string.IsNullOrWhiteSpace(tokenResponse.IdToken))
        {
            throw new InvalidOperationException("The token endpoint did not return the required OAuth tokens.");
        }

        return tokenResponse;
    }

    private OAuthResult BuildResult(OAuthTokenResponse tokenResponse)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenResponse.IdToken);

        if (!jwt.Audiences.Contains(_options.ClientId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The identity token audience does not match the configured client ID.");
        }

        if (jwt.ValidTo <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("The identity token has expired.");
        }

        var email = GetFirstClaim(jwt, "email", "preferred_username", "upn", "unique_name");
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("The identity token did not include an email or username claim.");
        }

        var displayName = GetFirstClaim(jwt, "name", "preferred_username", "email") ?? email;
        var userName = GetFirstClaim(jwt, "preferred_username", "upn", "email");
        var expiresAtUtc = tokenResponse.ExpiresIn > 0
            ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
            : jwt.ValidTo;

        return new OAuthResult(email.Trim(), displayName.Trim(), tokenResponse.AccessToken, expiresAtUtc, userName?.Trim());
    }

    private static string? GetFirstClaim(JwtSecurityToken jwt, params string[] claimTypes) =>
        claimTypes
            .Select(type => jwt.Claims.FirstOrDefault(claim => string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase))?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string TryReadError(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var error = root.TryGetProperty("error_description", out var errorDescription)
                ? errorDescription.GetString()
                : root.TryGetProperty("error", out var errorCode)
                    ? errorCode.GetString()
                    : null;
            return error ?? content;
        }
        catch (JsonException)
        {
            return content;
        }
    }

    private static HttpListener CreateListener(Uri redirectUri)
    {
        var prefix = $"{redirectUri.Scheme}://{redirectUri.Host}:{redirectUri.Port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        return listener;
    }

    private static void OpenBrowser(string authorizationUrl)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = authorizationUrl,
            UseShellExecute = true
        });
    }

    private static async Task WriteBrowserResponseAsync(HttpListenerResponse response, HttpStatusCode statusCode, string title, string message)
    {
        if (!response.OutputStream.CanWrite)
        {
            return;
        }

        var html = $"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <title>{WebUtility.HtmlEncode(title)}</title>
    <style>
        body {{ font-family: Segoe UI, Arial, sans-serif; margin: 40px; color: #172B4D; background: #F4F5F7; }}
        .card {{ max-width: 560px; margin: 0 auto; background: white; border-radius: 12px; padding: 32px; box-shadow: 0 8px 24px rgba(9,30,66,0.12); }}
        h1 {{ font-size: 22px; margin-bottom: 12px; }}
        p {{ line-height: 1.5; }}
    </style>
</head>
<body>
    <div class="card">
        <h1>{WebUtility.HtmlEncode(title)}</h1>
        <p>{message}</p>
    </div>
</body>
</html>
""";
        var buffer = Encoding.UTF8.GetBytes(html);
        response.StatusCode = (int)statusCode;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private static string NormalizePath(string path) =>
        string.IsNullOrWhiteSpace(path)
            ? "/"
            : path.TrimEnd('/').Length == 0 ? "/" : path.TrimEnd('/');

    private static void SafeStop(HttpListener listener)
    {
        try
        {
            if (listener.IsListening)
            {
                listener.Stop();
            }
        }
        catch
        {
        }
    }

    private static string GenerateCodeVerifier() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(64));

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string GenerateNonce() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class OAuthTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string IdToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}
