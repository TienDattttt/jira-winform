using JiraClone.Application.Abstractions;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Application.Versions;

public class VersionService : IVersionService
{
    private readonly IProjectVersionRepository _versions;
    private readonly IIssueRepository _issues;
    private readonly IProjectRepository _projects;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;

    public VersionService(
        IProjectVersionRepository versions,
        IIssueRepository issues,
        IProjectRepository projects,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork)
    {
        _versions = versions;
        _issues = issues;
        _projects = projects;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _currentUserContext = currentUserContext;
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<ProjectVersion>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default) =>
        _versions.GetByProjectAsync(projectId, cancellationToken);

    public async Task<ProjectVersion> CreateAsync(int projectId, string name, string? description, DateTime? releaseDate, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var project = await RequireProjectAsync(projectId, cancellationToken);
        var normalizedName = NormalizeName(name);
        EnsureUniqueName(project, normalizedName, null);

        var version = new ProjectVersion
        {
            ProjectId = projectId,
            Name = normalizedName,
            Description = NormalizeText(description),
            ReleaseDate = releaseDate,
            IsReleased = false
        };

        await _versions.AddAsync(version, cancellationToken);
        await AddProjectActivityAsync(projectId, ActivityActionType.Created, nameof(ProjectVersion.Name), null, version.Name, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return version;
    }

    public async Task<ProjectVersion?> UpdateAsync(int versionId, string name, string? description, DateTime? releaseDate, bool isReleased, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var version = await _versions.GetByIdAsync(versionId, cancellationToken);
        if (version is null)
        {
            return null;
        }

        var project = await RequireProjectAsync(version.ProjectId, cancellationToken);
        var normalizedName = NormalizeName(name);
        EnsureUniqueName(project, normalizedName, version.Id);
        var previousValue = $"{version.Name}|{version.IsReleased}|{version.ReleaseDate:yyyy-MM-dd}";

        version.Name = normalizedName;
        version.Description = NormalizeText(description);
        version.ReleaseDate = releaseDate;
        version.IsReleased = isReleased;
        version.UpdatedAtUtc = DateTime.UtcNow;

        await AddProjectActivityAsync(version.ProjectId, ActivityActionType.Updated, nameof(ProjectVersion.Name), previousValue, $"{version.Name}|{version.IsReleased}|{version.ReleaseDate:yyyy-MM-dd}", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return version;
    }

    public async Task<bool> DeleteAsync(int versionId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var version = await _versions.GetByIdAsync(versionId, cancellationToken);
        if (version is null)
        {
            return false;
        }

        await _versions.RemoveAsync(version, cancellationToken);
        await AddProjectActivityAsync(version.ProjectId, ActivityActionType.Deleted, nameof(ProjectVersion.Name), version.Name, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AssignToIssueAsync(int issueId, int? versionId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager, RoleCatalog.Developer);
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return false;
        }

        var version = versionId.HasValue ? await _versions.GetByIdAsync(versionId.Value, cancellationToken) : null;
        if (version is not null && version.ProjectId != issue.ProjectId)
        {
            throw new InvalidOperationException("Fix version must belong to the same project as the issue.");
        }

        var previousValue = issue.FixVersion?.Name;
        issue.FixVersionId = version?.Id;
        issue.FixVersion = version;
        issue.UpdatedAtUtc = DateTime.UtcNow;

        await AddIssueActivityAsync(issue, nameof(Issue.FixVersionId), previousValue, version?.Name, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ProjectVersion?> MarkReleasedAsync(int versionId, DateTime? releaseDate = null, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var version = await _versions.GetByIdAsync(versionId, cancellationToken);
        if (version is null)
        {
            return null;
        }

        version.IsReleased = true;
        version.ReleaseDate = releaseDate ?? version.ReleaseDate ?? DateTime.UtcNow;
        version.UpdatedAtUtc = DateTime.UtcNow;

        await AddProjectActivityAsync(version.ProjectId, ActivityActionType.Updated, nameof(ProjectVersion.IsReleased), "False", "True", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return version;
    }

    private async Task<Project> RequireProjectAsync(int projectId, CancellationToken cancellationToken)
    {
        return await _projects.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} was not found.");
    }

    private static void EnsureUniqueName(Project project, string normalizedName, int? currentVersionId)
    {
        var duplicate = project.Versions.Any(x => x.Id != currentVersionId && string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            throw new InvalidOperationException($"Version '{normalizedName}' already exists in this project.");
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Version name is required.");
        }

        return name.Trim();
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task AddProjectActivityAsync(int projectId, ActivityActionType actionType, string fieldName, string? oldValue, string? newValue, CancellationToken cancellationToken)
    {
        if (_currentUserContext.CurrentUser?.Id is not int actorUserId)
        {
            return;
        }

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = projectId,
            UserId = actorUserId,
            ActionType = actionType,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue
        }, cancellationToken);
    }

    private async Task AddIssueActivityAsync(Issue issue, string fieldName, string? oldValue, string? newValue, CancellationToken cancellationToken)
    {
        if (_currentUserContext.CurrentUser?.Id is not int actorUserId)
        {
            return;
        }

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = issue.ProjectId,
            IssueId = issue.Id,
            UserId = actorUserId,
            ActionType = ActivityActionType.Updated,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue
        }, cancellationToken);
    }
}
