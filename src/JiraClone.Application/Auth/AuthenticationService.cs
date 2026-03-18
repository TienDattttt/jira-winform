using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Auth;

public class AuthenticationService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(IUserRepository users, IPasswordHasher passwordHasher, ICurrentUserContext currentUserContext, ILogger<AuthenticationService>? logger = null)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _currentUserContext = currentUserContext;
        _logger = logger ?? NullLogger<AuthenticationService>.Instance;
    }

    public async Task<AuthResult> LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting authentication for user {UserName}.", userName);
        var user = await _users.GetByUserNameAsync(userName, cancellationToken);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Authentication failed for user {UserName}: user not found or inactive.", userName);
            return new AuthResult(false, "Invalid username or password.", null);
        }

        if (!_passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            _logger.LogWarning("Authentication failed for user {UserName}: invalid password.", userName);
            return new AuthResult(false, "Invalid username or password.", null);
        }

        _currentUserContext.Set(user);
        _logger.LogInformation("Authentication succeeded for user {UserId}.", user.Id);
        return new AuthResult(true, null, user);
    }
}
