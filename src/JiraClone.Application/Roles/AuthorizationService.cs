using JiraClone.Application.Abstractions;

namespace JiraClone.Application.Roles;

public class AuthorizationService : IAuthorizationService
{
    private readonly ICurrentUserContext _currentUserContext;

    public AuthorizationService(ICurrentUserContext currentUserContext)
    {
        _currentUserContext = currentUserContext;
    }

    public bool IsInRole(params string[] roleNames)
    {
        var user = _currentUserContext.CurrentUser;
        if (user is null)
        {
            return false;
        }

        return user.UserRoles.Any(x => roleNames.Contains(x.Role.Name, StringComparer.OrdinalIgnoreCase));
    }

    public void EnsureInRole(params string[] roleNames)
    {
        if (!IsInRole(roleNames))
        {
            throw new UnauthorizedAccessException("Current user does not have permission to perform this action.");
        }
    }
}
