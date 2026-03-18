using JiraClone.Application.Abstractions;
using JiraClone.Domain.Enums;
using JiraClone.Domain.Permissions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Permissions;

public class PermissionService : IPermissionService
{
    private readonly IProjectRepository _projects;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(IProjectRepository projects, ILogger<PermissionService>? logger = null)
    {
        _projects = projects;
        _logger = logger ?? NullLogger<PermissionService>.Instance;
    }

    public async Task<bool> HasPermissionAsync(int userId, int projectId, Permission permission, CancellationToken cancellationToken = default)
    {
        var permissions = await GetUserPermissionsAsync(userId, projectId, cancellationToken);
        var hasPermission = permissions.Contains(permission);
        _logger.LogDebug(
            "Permission check for user {UserId} on project {ProjectId} and permission {Permission}: {HasPermission}",
            userId,
            projectId,
            permission,
            hasPermission);
        return hasPermission;
    }

    public async Task<IReadOnlyList<Permission>> GetUserPermissionsAsync(int userId, int projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null || !project.IsActive)
        {
            return [];
        }

        var membership = project.Members.FirstOrDefault(member => member.UserId == userId);
        if (membership is null)
        {
            return [];
        }

        var grants = project.PermissionScheme?.Grants;
        if (grants is null || grants.Count == 0)
        {
            return PermissionDefaults.GetPermissionsForRole(membership.ProjectRole);
        }

        return grants
            .Where(grant => grant.ProjectRole == membership.ProjectRole)
            .Select(grant => grant.Permission)
            .Distinct()
            .OrderBy(permission => permission)
            .ToList();
    }
}
