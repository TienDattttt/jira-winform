using JiraClone.Application.Abstractions;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Application.Projects;

public class ProjectCommandService
{
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectCommandService(
        IProjectRepository projects,
        IUserRepository users,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _users = users;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _currentUserContext = currentUserContext;
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

        var oldName = project.Name;
        project.Name = name.Trim();
        project.Description = description;
        project.Category = category;
        project.Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        project.UpdatedAtUtc = DateTime.UtcNow;

        await AddProjectActivityAsync(project.Id, ActivityActionType.Updated, nameof(Project.Name), oldName, project.Name, cancellationToken);
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

        var previousRole = membership.ProjectRole;
        membership.ProjectRole = projectRole;
        await AddProjectActivityAsync(projectId, ActivityActionType.Updated, nameof(ProjectMember.ProjectRole), previousRole.ToString(), projectRole.ToString(), cancellationToken);
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

        await AddProjectActivityAsync(projectId, ActivityActionType.Updated, nameof(ProjectMember.UserId), null, user.DisplayName, cancellationToken);
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

        var memberName = membership.User.DisplayName;
        project!.Members.Remove(membership);
        await AddProjectActivityAsync(projectId, ActivityActionType.Updated, nameof(ProjectMember.UserId), memberName, null, cancellationToken);
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

        var oldValue = $"{column.Name}|{column.WipLimit}";
        column.Name = name.Trim();
        column.WipLimit = wipLimit;
        column.UpdatedAtUtc = DateTime.UtcNow;
        await AddProjectActivityAsync(projectId, ActivityActionType.Updated, nameof(BoardColumn.Name), oldValue, $"{column.Name}|{column.WipLimit}", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task AddProjectActivityAsync(int projectId, ActivityActionType actionType, string? fieldName, string? oldValue, string? newValue, CancellationToken cancellationToken)
    {
        var actorUserId = _currentUserContext.CurrentUser?.Id;
        if (!actorUserId.HasValue)
        {
            return;
        }

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = projectId,
            UserId = actorUserId.Value,
            ActionType = actionType,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue
        }, cancellationToken);
    }
}
