namespace JiraClone.Application.Auth;

public sealed class OAuthOptions
{
    public bool Enabled { get; set; }
    public string ProviderName { get; set; } = "SSO";
    public string AuthorizationEndpoint { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://localhost:8765/callback";
    public string[] Scopes { get; set; } = ["openid", "profile", "email"];

    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(AuthorizationEndpoint) &&
        !string.IsNullOrWhiteSpace(TokenEndpoint) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(RedirectUri) &&
        Scopes.Length > 0;
}
