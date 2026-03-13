using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;

namespace JiraClone.Application.Auth;

public class AuthenticationService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserContext _currentUserContext;

    public AuthenticationService(IUserRepository users, IPasswordHasher passwordHasher, ICurrentUserContext currentUserContext)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _currentUserContext = currentUserContext;
    }

    public async Task<AuthResult> LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByUserNameAsync(userName, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return new AuthResult(false, "Invalid username or password.", null);
        }

        if (!_passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            return new AuthResult(false, "Invalid username or password.", null);
        }

        _currentUserContext.Set(user);
        return new AuthResult(true, null, user);
    }
}
