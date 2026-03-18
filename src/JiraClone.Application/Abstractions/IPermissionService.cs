using JiraClone.Domain.Enums;

namespace JiraClone.Application.Abstractions;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(int userId, int projectId, Permission permission, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Permission>> GetUserPermissionsAsync(int userId, int projectId, CancellationToken cancellationToken = default);
}
