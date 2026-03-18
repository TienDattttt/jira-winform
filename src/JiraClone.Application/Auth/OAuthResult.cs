namespace JiraClone.Application.Auth;

public sealed record OAuthResult(
    string Email,
    string DisplayName,
    string AccessToken,
    DateTime ExpiresAtUtc,
    string? UserName = null);
