namespace JiraClone.Application.Auth;

public interface IOAuthService
{
    Task<OAuthResult> AuthenticateAsync(CancellationToken cancellationToken = default);
}
