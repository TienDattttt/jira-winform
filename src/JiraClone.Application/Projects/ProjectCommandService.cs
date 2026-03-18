using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.Domain.Permissions;
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
    private readonly IIssueRepository _issues;
    private readonly ISprintRepository _sprints;
    private readonly IAuthorizationService _authorization;
    private readonly IPermissionService _permissionService;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IWebhookDispatcher _webhookDispatcher;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProjectCommandService> _logger;

    public ProjectCommandService(
        IProjectRepository projects,
        IUserRepository users,
        IIssueRepository issues,
        ISprintRepository sprints,
        IAuthorizationService authorization,
        IPermissionService permissionService,
        IActivityLogRepository activityLogs,
        IWebhookDispatcher webhookDispatcher,
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork,
        ILogger<ProjectCommandService>? logger = null)
    {
        _projects = projects;
        _users = users;
        _issues = issues;
        _sprints = sprints;
        _authorization = authorization;
        _permissionService = permissionService;
        _activityLogs = activityLogs;
        _webhookDispatcher = webhookDispatcher;
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
        project.PermissionScheme = CreateDefaultPermissionScheme(now);

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

    public async Task<Project?> UpdateProjectAsync(int projectId, string name, string? description, ProjectCategory category, BoardType boardType, string? url, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(projectId, Permission.ManageProject, cancellationToken);
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

        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        var normalizedUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        var oldName = project.Name;
        var oldDescription = project.Description;
        var oldCategory = project.Category;
        var oldBoardType = project.BoardType;
        var oldUrl = project.Url;

        project.Name = normalizedName;
        project.Description = normalizedDescription;
        project.Category = category;
        project.BoardType = boardType;
        project.Url = normalizedUrl;

        var hasChanges = !string.Equals(oldName, project.Name, StringComparison.Ordinal)
            || !string.Equals(oldDescription, project.Description, StringComparison.Ordinal)
            || oldCategory != project.Category
            || oldBoardType != project.BoardType
            || !string.Equals(oldUrl, project.Url, StringComparison.Ordinal);

        if (!hasChanges)
        {
            return project;
        }

        project.UpdatedAtUtc = DateTime.UtcNow;

        if (!string.Equals(oldName, project.Name, StringComparison.Ordinal))
        {
            await AddProjectActivityAsync(project.Id, ActivityActionType.Updated, nameof(Project.Name), oldName, project.Name, cancellationToken);
        }

        if (!string.Equals(oldDescription, project.Description, StringComparison.Ordinal))
        {
            await AddProjectActivityAsync(project.Id, ActivityActionType.Updated, nameof(Project.Description), oldDescription, project.Description, cancellationToken);
        }

        if (oldCategory != project.Category)
        {
            await AddProjectActivityAsync(project.Id, ActivityActionType.Updated, nameof(Project.Category), oldCategory.ToString(), project.Category.ToString(), cancellationToken);
        }

        if (oldBoardType != project.BoardType)
        {
            await AddProjectActivityAsync(project.Id, ActivityActionType.Updated, nameof(Project.BoardType), oldBoardType.ToString(), project.BoardType.ToString(), cancellationToken);
        }

        if (!string.Equals(oldUrl, project.Url, StringComparison.Ordinal))
        {
            await AddProjectActivityAsync(project.Id, ActivityActionType.Updated, nameof(Project.Url), oldUrl, project.Url, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _webhookDispatcher.DispatchAsync(project.Id, WebhookEventType.ProjectUpdated, CreateProjectWebhookPayload(project, "ProjectUpdated"), cancellationToken);
        return project;
    }

    public async Task<bool> ArchiveProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAdminAsync(projectId, cancellationToken);
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

    public async Task<bool> DeleteProjectAsync(int projectId, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAdminAsync(projectId, cancellationToken);

        var currentUserId = _currentUserContext.CurrentUser?.Id;
        if (currentUserId.HasValue && currentUserId.Value != userId)
        {
            throw new ValidationException("Project deletion must be performed by the currently signed-in user.");
        }

        var project = await _projects.GetDeleteSnapshotAsync(projectId, cancellationToken);
        if (project is null)
        {
            return false;
        }

        var sprints = await _sprints.GetAllByProjectIdAsync(projectId, cancellationToken);
        if (sprints.Any(x => x.State == SprintState.Active && !x.IsDeleted))
        {
            throw new ValidationException("Close the active sprint before deleting this project.");
        }

        var issues = await _issues.GetAllByProjectIdAsync(projectId, cancellationToken);
        var now = DateTime.UtcNow;
        var deletedIssueCount = 0;
        var deletedCommentCount = 0;
        var deletedAttachmentCount = 0;

        foreach (var issue in issues)
        {
            if (!issue.IsDeleted)
            {
                issue.IsDeleted = true;
                issue.UpdatedAtUtc = now;
                deletedIssueCount++;
            }

            foreach (var comment in issue.Comments)
            {
                if (comment.IsDeleted)
                {
                    continue;
                }

                comment.IsDeleted = true;
                comment.UpdatedAtUtc = now;
                deletedCommentCount++;
            }

            foreach (var attachment in issue.Attachments)
            {
                if (attachment.IsDeleted)
                {
                    continue;
                }

                attachment.IsDeleted = true;
                attachment.UpdatedAtUtc = now;
                deletedAttachmentCount++;
            }
        }

        var deletedSprintCount = 0;
        foreach (var sprint in sprints)
        {
            if (sprint.IsDeleted)
            {
                continue;
            }

            sprint.IsDeleted = true;
            sprint.UpdatedAtUtc = now;
            deletedSprintCount++;
        }

        var auditMetadata = JsonSerializer.Serialize(new
        {
            ProjectId = project.Id,
            project.Key,
            project.Name,
            DeletedIssueCount = deletedIssueCount,
            DeletedCommentCount = deletedCommentCount,
            DeletedAttachmentCount = deletedAttachmentCount,
            DeletedSprintCount = deletedSprintCount,
            DeletedMemberCount = project.Members.Count,
            DeletedBoardColumnCount = project.BoardColumns.Count,
            DeletedLabelCount = project.Labels.Count,
            DeletedComponentCount = project.Components.Count,
            DeletedVersionCount = project.Versions.Count,
            DeletedWorkflowCount = project.WorkflowDefinitions.Count,
            DeletedPermissionGrantCount = project.PermissionScheme?.Grants.Count ?? 0,
            DeletedSavedFilterCount = project.SavedFilters.Count,
            DeletedByUserId = userId,
            DeletedAtUtc = now,
        });

        await AddProjectActivityAsync(projectId, ActivityActionType.Deleted, nameof(Project), project.Key, project.Name, cancellationToken, userId, auditMetadata);
        _logger.LogWarning(
            "Project {ProjectId} ({ProjectKey}) delete confirmed by user {UserId}. Cascade summary: {AuditMetadata}",
            project.Id,
            project.Key,
            userId,
            auditMetadata);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _projects.DeleteAsync(project, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Project {ProjectId} ({ProjectKey}) was deleted successfully by user {UserId}.", project.Id, project.Key, userId);
        return true;
    }

    public async Task<bool> UpdateMemberRoleAsync(int projectId, int userId, ProjectRole projectRole, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(projectId, Permission.ManageMembers, cancellationToken);
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
        await EnsurePermissionAsync(projectId, Permission.ManageMembers, cancellationToken);
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
        await EnsurePermissionAsync(projectId, Permission.ManageMembers, cancellationToken);
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
        await EnsurePermissionAsync(projectId, Permission.ManageBoard, cancellationToken);
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
        await _webhookDispatcher.DispatchAsync(projectId, WebhookEventType.ProjectUpdated, CreateProjectWebhookPayload(project!, "BoardColumnUpdated"), cancellationToken);
        return true;
    }

    public async Task<bool> UpdatePermissionSchemeAsync(int projectId, string name, IReadOnlyCollection<PermissionGrantInput> grants, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAdminAsync(projectId, cancellationToken);
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return false;
        }

        var normalizedName = string.IsNullOrWhiteSpace(name) ? PermissionDefaults.DefaultSchemeName : name.Trim();
        var scheme = project.PermissionScheme;
        if (scheme is null)
        {
            scheme = new PermissionScheme
            {
                ProjectId = projectId,
                Name = normalizedName,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };
            project.PermissionScheme = scheme;
        }
        else
        {
            scheme.Name = normalizedName;
            scheme.UpdatedAtUtc = DateTime.UtcNow;
            scheme.Grants.Clear();
        }

        foreach (var grant in (grants ?? Array.Empty<PermissionGrantInput>())
                     .DistinctBy(x => new { x.Permission, x.ProjectRole })
                     .OrderBy(x => x.Permission)
                     .ThenBy(x => x.ProjectRole))
        {
            scheme.Grants.Add(new PermissionGrant
            {
                Permission = grant.Permission,
                ProjectRole = grant.ProjectRole,
            });
        }

        await AddProjectActivityAsync(
            projectId,
            ActivityActionType.Updated,
            nameof(PermissionScheme),
            null,
            normalizedName,
            cancellationToken,
            metadataJson: JsonSerializer.Serialize(new
            {
                SchemeName = normalizedName,
                Grants = scheme.Grants.Select(x => new { x.Permission, x.ProjectRole }).ToArray()
            }));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _webhookDispatcher.DispatchAsync(projectId, WebhookEventType.ProjectUpdated, CreateProjectWebhookPayload(project, "PermissionSchemeUpdated"), cancellationToken);
        return true;
    }

    private static object CreateProjectWebhookPayload(Project project, string reason)
    {
        return new
        {
            project.Id,
            project.Key,
            project.Name,
            project.Description,
            project.Category,
            project.BoardType,
            project.Url,
            project.IsActive,
            Reason = reason,
            project.UpdatedAtUtc,
        };
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

    private static PermissionScheme CreateDefaultPermissionScheme(DateTime now)
    {
        var scheme = new PermissionScheme
        {
            Name = PermissionDefaults.DefaultSchemeName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        foreach (var grant in PermissionDefaults.GetDefaultGrants())
        {
            scheme.Grants.Add(new PermissionGrant
            {
                Permission = grant.Permission,
                ProjectRole = grant.ProjectRole,
            });
        }

        return scheme;
    }

    private async Task EnsurePermissionAsync(int projectId, Permission permission, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserContext.CurrentUser?.Id ?? throw new InvalidOperationException("No user is currently logged in.");
        if (!await _permissionService.HasPermissionAsync(currentUserId, projectId, permission, cancellationToken))
        {
            throw new UnauthorizedAccessException("Current user does not have permission to perform this action.");
        }
    }

    private async Task EnsureProjectAdminAsync(int projectId, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserContext.CurrentUser?.Id ?? throw new InvalidOperationException("No user is currently logged in.");
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        var role = project?.Members.FirstOrDefault(member => member.UserId == currentUserId)?.ProjectRole;
        if (role != ProjectRole.Admin)
        {
            throw new UnauthorizedAccessException("Current user does not have permission to perform this action.");
        }
    }

    private async Task AddProjectActivityAsync(
        int projectId,
        ActivityActionType actionType,
        string? fieldName,
        string? oldValue,
        string? newValue,
        CancellationToken cancellationToken,
        int? actorUserId = null,
        string? metadataJson = null)
    {
        var effectiveActorUserId = actorUserId ?? _currentUserContext.CurrentUser?.Id;
        if (!effectiveActorUserId.HasValue)
        {
            return;
        }

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = projectId,
            UserId = effectiveActorUserId.Value,
            ActionType = actionType,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            MetadataJson = metadataJson,
        }, cancellationToken);
    }
}