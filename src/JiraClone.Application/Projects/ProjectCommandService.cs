using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Projects;

public class ProjectCommandService : IProjectCommandService
{
    private static readonly (string Name, string Color, StatusCategory Category)[] DefaultWorkflowStatuses =
    [
        ("Backlog", "#42526E", StatusCategory.ToDo),
        ("Selected", "#4C9AFF", StatusCategory.ToDo),
        ("In Progress", "#0052CC", StatusCategory.InProgress),
        ("Done", "#36B37E", StatusCategory.Done)
    ];

    private static readonly string[] DefaultEditableRoles = [RoleCatalog.Admin, RoleCatalog.ProjectManager, RoleCatalog.Developer];

    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProjectCommandService> _logger;

    public ProjectCommandService(
        IProjectRepository projects,
        IUserRepository users,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork,
        ILogger<ProjectCommandService>? logger = null)
    {
        _projects = projects;
        _users = users;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _currentUserContext = currentUserContext;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<ProjectCommandService>.Instance;
    }

    public async Task<Project> CreateProjectAsync(string name, string key, ProjectCategory category, string? description, IReadOnlyCollection<ProjectMemberInput> members, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);

        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ValidationException("Project name is required.");
        }

        var normalizedKey = NormalizeProjectKey(key);
        if (!Regex.IsMatch(normalizedKey, "^[A-Z]{2,10}$", RegexOptions.CultureInvariant))
        {
            throw new ValidationException("Project key must match [A-Z]{2,10}.");
        }

        if (await _projects.ExistsByKeyAsync(normalizedKey, cancellationToken: cancellationToken))
        {
            throw new ValidationException("Project key already exists.");
        }

        var actor = _currentUserContext.CurrentUser ?? throw new InvalidOperationException("No user is currently logged in.");
        var now = DateTime.UtcNow;
        var project = new Project
        {
            Key = normalizedKey,
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Category = category,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var workflow = new WorkflowDefinition
        {
            Name = "Default Workflow",
            IsDefault = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        project.WorkflowDefinitions.Add(workflow);

        foreach (var template in DefaultWorkflowStatuses.Select((value, index) => new { value, index }))
        {
            var status = new WorkflowStatus
            {
                Name = template.value.Name,
                Color = template.value.Color,
                Category = template.value.Category,
                DisplayOrder = template.index + 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            workflow.Statuses.Add(status);
            project.BoardColumns.Add(new BoardColumn
            {
                Name = status.Name,
                WorkflowStatus = status,
                DisplayOrder = template.index + 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        var allowedRoles = (await _users.GetRolesAsync(cancellationToken))
            .Where(x => DefaultEditableRoles.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(x => x.Name)
            .ToList();
        var statuses = workflow.Statuses.OrderBy(x => x.DisplayOrder).ToList();
        foreach (var fromStatus in statuses)
        {
            foreach (var toStatus in statuses.Where(x => x != fromStatus))
            {
                workflow.Transitions.Add(new WorkflowTransition
                {
                    Name = $"{fromStatus.Name} to {toStatus.Name}",
                    FromStatus = fromStatus,
                    ToStatus = toStatus,
                    AllowedRoles = allowedRoles.ToList(),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });
            }
        }

        var uniqueMembers = (members ?? Array.Empty<ProjectMemberInput>())
            .Where(x => x.UserId > 0)
            .GroupBy(x => x.UserId)
            .Select(x => x.First())
            .ToList();

        AddProjectMember(project, actor, ProjectRole.Admin, now);

        foreach (var member in uniqueMembers)
        {
            if (member.UserId == actor.Id)
            {
                continue;
            }

            var user = await _users.GetByIdAsync(member.UserId, cancellationToken);
            if (user is null || !user.IsActive)
            {
                throw new ValidationException($"User {member.UserId} is not available for project membership.");
            }

            AddProjectMember(project, user, member.ProjectRole, now);
        }

        await _projects.AddAsync(project, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await AddProjectActivityAsync(project.Id, ActivityActionType.Created, nameof(Project.Name), null, project.Name, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await _projects.GetByIdAsync(project.Id, cancellationToken) ?? project;
    }

    public Task<bool> ProjectKeyExistsAsync(string key, int? excludeProjectId = null, CancellationToken cancellationToken = default)
    {
        return _projects.ExistsByKeyAsync(NormalizeProjectKey(key), excludeProjectId, cancellationToken);
    }

    public async Task<Project?> UpdateProjectAsync(int projectId, string name, string? description, ProjectCategory category, string? url, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ValidationException("Project name is required.");
        }

        var oldName = project.Name;
        project.Name = normalizedName;
        project.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        project.Category = category;
        project.Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        project.UpdatedAtUtc = DateTime.UtcNow;

        await AddProjectActivityAsync(project.Id, ActivityActionType.Updated, nameof(Project.Name), oldName, project.Name, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task<bool> ArchiveProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin);
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null || !project.IsActive)
        {
            return false;
        }

        project.IsActive = false;
        project.UpdatedAtUtc = DateTime.UtcNow;

        await AddProjectActivityAsync(projectId, ActivityActionType.Updated, nameof(Project.IsActive), bool.TrueString, bool.FalseString, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
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
        if (project is null || user is null || !user.IsActive || project.Members.Any(x => x.UserId == userId))
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

        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ValidationException("Column name is required.");
        }

        var oldValue = $"{column.Name}|{column.WipLimit}";
        column.Name = normalizedName;
        column.WipLimit = wipLimit;
        column.UpdatedAtUtc = DateTime.UtcNow;
        if (column.WorkflowStatus is not null)
        {
            column.WorkflowStatus.Name = normalizedName;
            column.WorkflowStatus.UpdatedAtUtc = DateTime.UtcNow;
        }

        await AddProjectActivityAsync(projectId, ActivityActionType.Updated, nameof(BoardColumn.Name), oldValue, $"{column.Name}|{column.WipLimit}", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string NormalizeProjectKey(string? key)
    {
        return (key ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static void AddProjectMember(Project project, User user, ProjectRole role, DateTime joinedAtUtc)
    {
        if (project.Members.Any(x => x.UserId == user.Id))
        {
            return;
        }

        project.Members.Add(new ProjectMember
        {
            UserId = user.Id,
            User = user,
            ProjectRole = role,
            JoinedAtUtc = joinedAtUtc,
        });
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




