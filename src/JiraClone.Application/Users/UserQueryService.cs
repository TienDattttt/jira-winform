using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;

namespace JiraClone.Application.Users;

public class UserQueryService
{
    private readonly IUserRepository _users;

    public UserQueryService(IUserRepository users)
    {
        _users = users;
    }

    public Task<IReadOnlyList<User>> GetProjectUsersAsync(int projectId, CancellationToken cancellationToken = default) =>
        _users.GetProjectUsersAsync(projectId, cancellationToken);
}
