using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Users;

public class UserQueryService
{
    private readonly IUserRepository _users;
    private readonly ILogger<UserQueryService> _logger;

    public UserQueryService(IUserRepository users, ILogger<UserQueryService>? logger = null)
    {
        _users = users;
        _logger = logger ?? NullLogger<UserQueryService>.Instance;
    }

    public Task<IReadOnlyList<User>> GetProjectUsersAsync(int projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading users for project {ProjectId}.", projectId);
        return _users.GetProjectUsersAsync(projectId, cancellationToken);
    }
}
