using JiraClone.Application.Abstractions;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Projects;

public class ProjectCommandService
{
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly IAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectCommandService(
        IProjectRepository projects,
        IUserRepository users,
        IAuthorizationService authorization,
        IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _users = users;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
    }

    public async Task<Project?> UpdateProjectAsync(int projectId, string name, string? description, ProjectCategory category, string? url, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        project.Name = name.Trim();
        project.Description = description;
        project.Category = category;
        project.Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task<bool> UpdateMemberRoleAsync(int projectId, int userId, ProjectRole projectRole, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        var membership = project?.Members.FirstOrDefault(x => x.UserId == userId);
        if (membership is null)
        {
            return false;
        }

        membership.ProjectRole = projectRole;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AddMemberAsync(int projectId, int userId, ProjectRole projectRole, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (project is null || user is null || project.Members.Any(x => x.UserId == userId))
        {
            return false;
        }

        project.Members.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            User = user,
            ProjectRole = projectRole,
            JoinedAtUtc = DateTime.UtcNow
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        var membership = project?.Members.FirstOrDefault(x => x.UserId == userId);
        if (membership is null)
        {
            return false;
        }

        project!.Members.Remove(membership);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateBoardColumnAsync(int projectId, int boardColumnId, string name, int? wipLimit, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        var column = project?.BoardColumns.FirstOrDefault(x => x.Id == boardColumnId);
        if (column is null)
        {
            return false;
        }

        column.Name = name.Trim();
        column.WipLimit = wipLimit;
        column.UpdatedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
