using JiraClone.Application.Abstractions;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Application.Components;

public class ComponentService : IComponentService
{
    private readonly IComponentRepository _components;
    private readonly IIssueRepository _issues;
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;

    public ComponentService(
        IComponentRepository components,
        IIssueRepository issues,
        IProjectRepository projects,
        IUserRepository users,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork)
    {
        _components = components;
        _issues = issues;
        _projects = projects;
        _users = users;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _currentUserContext = currentUserContext;
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<Component>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default) =>
        _components.GetByProjectAsync(projectId, cancellationToken);

    public async Task<Component> CreateAsync(int projectId, string name, string? description, int? leadUserId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var project = await RequireProjectAsync(projectId, cancellationToken);
        var normalizedName = NormalizeName(name);
        EnsureUniqueName(project, normalizedName, null);
        var leadUser = leadUserId.HasValue ? await _users.GetByIdAsync(leadUserId.Value, cancellationToken) : null;

        var component = new Component
        {
            ProjectId = projectId,
            Name = normalizedName,
            Description = NormalizeText(description),
            LeadUserId = leadUser?.Id,
            LeadUser = leadUser
        };

        await _components.AddAsync(component, cancellationToken);
        await AddProjectActivityAsync(projectId, ActivityActionType.Created, nameof(Component.Name), null, component.Name, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return component;
    }

    public async Task<Component?> UpdateAsync(int componentId, string name, string? description, int? leadUserId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var component = await _components.GetByIdAsync(componentId, cancellationToken);
        if (component is null)
        {
            return null;
        }

        var project = await RequireProjectAsync(component.ProjectId, cancellationToken);
        var normalizedName = NormalizeName(name);
        EnsureUniqueName(project, normalizedName, component.Id);
        var leadUser = leadUserId.HasValue ? await _users.GetByIdAsync(leadUserId.Value, cancellationToken) : null;
        var previousValue = $"{component.Name}|{component.LeadUser?.DisplayName ?? string.Empty}";

        component.Name = normalizedName;
        component.Description = NormalizeText(description);
        component.LeadUserId = leadUser?.Id;
        component.LeadUser = leadUser;
        component.UpdatedAtUtc = DateTime.UtcNow;

        await AddProjectActivityAsync(component.ProjectId, ActivityActionType.Updated, nameof(Component.Name), previousValue, $"{component.Name}|{component.LeadUser?.DisplayName ?? string.Empty}", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return component;
    }

    public async Task<bool> DeleteAsync(int componentId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var component = await _components.GetByIdAsync(componentId, cancellationToken);
        if (component is null)
        {
            return false;
        }

        await _components.RemoveAsync(component, cancellationToken);
        await AddProjectActivityAsync(component.ProjectId, ActivityActionType.Deleted, nameof(Component.Name), component.Name, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AssignToIssueAsync(int issueId, int? componentId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager, RoleCatalog.Developer);
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return false;
        }

        var projectComponents = await _components.GetByProjectAsync(issue.ProjectId, cancellationToken);
        var selected = componentId.HasValue ? projectComponents.FirstOrDefault(x => x.Id == componentId.Value) : null;
        var previousValue = string.Join(", ", issue.IssueComponents.Select(x => x.Component.Name).OrderBy(x => x));

        issue.IssueComponents.Clear();
        if (selected is not null)
        {
            issue.IssueComponents.Add(new IssueComponent
            {
                Issue = issue,
                IssueId = issue.Id,
                Component = selected,
                ComponentId = selected.Id
            });
        }

        issue.UpdatedAtUtc = DateTime.UtcNow;
        var nextValue = string.Join(", ", issue.IssueComponents.Select(x => x.Component.Name).OrderBy(x => x));
        await AddIssueActivityAsync(issue, nameof(Component.Name), previousValue, nextValue, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Project> RequireProjectAsync(int projectId, CancellationToken cancellationToken)
    {
        return await _projects.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} was not found.");
    }

    private static void EnsureUniqueName(Project project, string normalizedName, int? currentComponentId)
    {
        var duplicate = project.Components.Any(x => x.Id != currentComponentId && string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            throw new InvalidOperationException($"Component '{normalizedName}' already exists in this project.");
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Component name is required.");
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
