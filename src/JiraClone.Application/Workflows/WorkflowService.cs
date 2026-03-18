using System.Text.Json;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Workflows;

public class WorkflowService : IWorkflowService
{
    private static readonly string[] DefaultEditableRoles = [RoleCatalog.Admin, RoleCatalog.ProjectManager, RoleCatalog.Developer];

    private readonly IWorkflowRepository _workflows;
    private readonly IIssueRepository _issues;
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        IWorkflowRepository workflows,
        IIssueRepository issues,
        IProjectRepository projects,
        IUserRepository users,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork,
        ILogger<WorkflowService>? logger = null)
    {
        _workflows = workflows;
        _issues = issues;
        _projects = projects;
        _users = users;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _currentUserContext = currentUserContext;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<WorkflowService>.Instance;
    }

    public async Task<WorkflowDefinitionDto?> GetDefaultWorkflowAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var workflow = await _workflows.GetDefaultByProjectAsync(projectId, cancellationToken);
        return workflow is null ? null : Map(workflow);
    }

    public async Task<IReadOnlyList<WorkflowStatusOptionDto>> GetAllowedTransitionsAsync(int issueId, int userId, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return [];
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        var roleNames = user?.UserRoles.Select(x => x.Role.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var transitions = await _workflows.GetTransitionsFromStatusAsync(issue.WorkflowStatus.WorkflowDefinitionId, issue.WorkflowStatusId, cancellationToken);

        return transitions
            .Where(x => CanExecute(x, roleNames))
            .Select(x => Map(x.ToStatus))
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.DisplayOrder)
            .ToList();
    }

    public async Task<WorkflowTransitionResult> ExecuteTransitionAsync(int issueId, int toStatusId, int userId, decimal? boardPosition = null, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager, RoleCatalog.Developer);

        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return new WorkflowTransitionResult(false, null, null, 0m);
        }

        if (issue.WorkflowStatusId == toStatusId)
        {
            return new WorkflowTransitionResult(true, issue.WorkflowStatus, issue.WorkflowStatus, issue.BoardPosition);
        }

        var transition = await _workflows.GetTransitionAsync(issue.WorkflowStatus.WorkflowDefinitionId, issue.WorkflowStatusId, toStatusId, cancellationToken)
            ?? throw new InvalidOperationException("The selected transition is not available in the active workflow.");

        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("The user executing this transition was not found.");
        var roleNames = user.UserRoles.Select(x => x.Role.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!CanExecute(transition, roleNames))
        {
            throw new InvalidOperationException("The current user is not allowed to execute this transition.");
        }

        var previousStatus = issue.WorkflowStatus;
        var nextPosition = boardPosition ?? await _issues.GetNextBoardPositionAsync(issue.ProjectId, toStatusId, cancellationToken);
        issue.MoveTo(toStatusId, nextPosition);
        issue.WorkflowStatus = transition.ToStatus;

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = issue.ProjectId,
            IssueId = issue.Id,
            UserId = userId,
            ActionType = ActivityActionType.StatusChanged,
            FieldName = nameof(Issue.WorkflowStatusId),
            OldValue = previousStatus.Name,
            NewValue = transition.ToStatus.Name,
            MetadataJson = BuildTransitionMetadata(previousStatus, transition.ToStatus)
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new WorkflowTransitionResult(true, previousStatus, transition.ToStatus, nextPosition);
    }

    public async Task<WorkflowStatus> CreateStatusAsync(int projectId, string name, string color, StatusCategory category, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var project = await _projects.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException("Project was not found.");
        var workflow = GetDefaultWorkflow(project) ?? throw new InvalidOperationException("Default workflow was not found.");

        var normalizedName = RequireName(name, "Status name is required.");
        if (workflow.Statuses.Any(x => string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A workflow status with this name already exists.");
        }

        var now = DateTime.UtcNow;
        var status = new WorkflowStatus
        {
            Name = normalizedName,
            Color = NormalizeColor(color),
            Category = category,
            DisplayOrder = workflow.Statuses.Count == 0 ? 1 : workflow.Statuses.Max(x => x.DisplayOrder) + 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        workflow.Statuses.Add(status);
        project.BoardColumns.Add(new BoardColumn
        {
            Name = status.Name,
            WorkflowStatus = status,
            DisplayOrder = project.BoardColumns.Count == 0 ? 1 : project.BoardColumns.Max(x => x.DisplayOrder) + 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await AddProjectActivityAsync(project.Id, ActivityActionType.Updated, nameof(WorkflowStatus.Name), null, status.Name, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return status;
    }

    public async Task<WorkflowStatus?> UpdateStatusAsync(int workflowStatusId, string name, string color, StatusCategory category, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var existing = await _workflows.GetStatusByIdAsync(workflowStatusId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var project = await _projects.GetByIdAsync(existing.WorkflowDefinition.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("Project was not found.");
        var workflow = GetDefaultWorkflow(project) ?? throw new InvalidOperationException("Default workflow was not found.");
        var status = workflow.Statuses.First(x => x.Id == workflowStatusId);
        var normalizedName = RequireName(name, "Status name is required.");

        if (workflow.Statuses.Any(x => x.Id != workflowStatusId && string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A workflow status with this name already exists.");
        }

        var oldValue = $"{status.Name}|{status.Color}|{status.Category}";
        status.Name = normalizedName;
        status.Color = NormalizeColor(color);
        status.Category = category;
        status.UpdatedAtUtc = DateTime.UtcNow;

        foreach (var column in project.BoardColumns.Where(x => x.WorkflowStatusId == workflowStatusId || x.WorkflowStatus?.Id == workflowStatusId))
        {
            column.Name = status.Name;
            column.UpdatedAtUtc = DateTime.UtcNow;
        }

        await AddProjectActivityAsync(project.Id, ActivityActionType.Updated, nameof(WorkflowStatus.Name), oldValue, $"{status.Name}|{status.Color}|{status.Category}", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return status;
    }

    public async Task<bool> DeleteStatusAsync(int workflowStatusId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var existing = await _workflows.GetStatusByIdAsync(workflowStatusId, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var project = await _projects.GetByIdAsync(existing.WorkflowDefinition.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("Project was not found.");
        var workflow = GetDefaultWorkflow(project) ?? throw new InvalidOperationException("Default workflow was not found.");
        if (workflow.Statuses.Count <= 1)
        {
            throw new InvalidOperationException("A workflow must keep at least one status.");
        }

        var inUse = (await _issues.GetProjectIssuesAsync(project.Id, cancellationToken)).Any(x => x.WorkflowStatusId == workflowStatusId);
        if (inUse)
        {
            throw new InvalidOperationException("Cannot delete a workflow status that is still assigned to issues.");
        }

        var status = workflow.Statuses.First(x => x.Id == workflowStatusId);
        var transitionsToRemove = workflow.Transitions.Where(x => x.FromStatusId == workflowStatusId || x.ToStatusId == workflowStatusId).ToList();
        foreach (var transition in transitionsToRemove)
        {
            workflow.Transitions.Remove(transition);
        }

        foreach (var column in project.BoardColumns.Where(x => x.WorkflowStatusId == workflowStatusId || x.WorkflowStatus?.Id == workflowStatusId).ToList())
        {
            project.BoardColumns.Remove(column);
        }

        workflow.Statuses.Remove(status);
        NormalizeOrdering(workflow.Statuses, project.BoardColumns);

        await AddProjectActivityAsync(project.Id, ActivityActionType.Updated, nameof(WorkflowStatus.Name), status.Name, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<WorkflowTransition?> CreateTransitionAsync(int projectId, int fromStatusId, int toStatusId, string name, IReadOnlyCollection<string> allowedRoleNames, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        if (fromStatusId == toStatusId)
        {
            throw new InvalidOperationException("A transition must point to a different status.");
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException("Project was not found.");
        var workflow = GetDefaultWorkflow(project) ?? throw new InvalidOperationException("Default workflow was not found.");
        var fromStatus = workflow.Statuses.FirstOrDefault(x => x.Id == fromStatusId)
            ?? throw new InvalidOperationException("Source status was not found.");
        var toStatus = workflow.Statuses.FirstOrDefault(x => x.Id == toStatusId)
            ?? throw new InvalidOperationException("Target status was not found.");

        if (workflow.Transitions.Any(x => x.FromStatusId == fromStatusId && x.ToStatusId == toStatusId))
        {
            throw new InvalidOperationException("This workflow transition already exists.");
        }

        var transition = new WorkflowTransition
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"{fromStatus.Name} to {toStatus.Name}" : name.Trim(),
            FromStatus = fromStatus,
            ToStatus = toStatus,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            AllowedRoles = await ResolveAllowedRolesAsync(allowedRoleNames, cancellationToken)
        };

        workflow.Transitions.Add(transition);
        await AddProjectActivityAsync(project.Id, ActivityActionType.Updated, nameof(WorkflowTransition.Name), null, transition.Name, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return transition;
    }

    public async Task<WorkflowTransition?> UpdateTransitionAsync(int transitionId, string name, IReadOnlyCollection<string> allowedRoleNames, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var existing = await _workflows.GetTransitionByIdAsync(transitionId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var workflow = await _workflows.GetByIdAsync(existing.FromStatus.WorkflowDefinitionId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow was not found.");
        var transition = workflow.Transitions.First(x => x.Id == transitionId);
        var oldValue = transition.Name;
        transition.Name = string.IsNullOrWhiteSpace(name) ? transition.Name : name.Trim();
        transition.UpdatedAtUtc = DateTime.UtcNow;
        transition.AllowedRoles.Clear();
        foreach (var role in await ResolveAllowedRolesAsync(allowedRoleNames, cancellationToken))
        {
            transition.AllowedRoles.Add(role);
        }

        await AddProjectActivityAsync(workflow.ProjectId, ActivityActionType.Updated, nameof(WorkflowTransition.Name), oldValue, transition.Name, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return transition;
    }

    public async Task<bool> DeleteTransitionAsync(int transitionId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var existing = await _workflows.GetTransitionByIdAsync(transitionId, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var workflow = await _workflows.GetByIdAsync(existing.FromStatus.WorkflowDefinitionId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow was not found.");
        var transition = workflow.Transitions.First(x => x.Id == transitionId);
        workflow.Transitions.Remove(transition);
        await AddProjectActivityAsync(workflow.ProjectId, ActivityActionType.Updated, nameof(WorkflowTransition.Name), transition.Name, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static WorkflowDefinitionDto Map(WorkflowDefinition workflow)
    {
        var statuses = workflow.Statuses
            .OrderBy(x => x.DisplayOrder)
            .Select(Map)
            .ToList();
        var transitions = workflow.Transitions
            .OrderBy(x => x.FromStatus.DisplayOrder)
            .ThenBy(x => x.ToStatus.DisplayOrder)
            .Select(x => new WorkflowTransitionDto(
                x.Id,
                x.FromStatusId,
                x.FromStatus.Name,
                x.ToStatusId,
                x.ToStatus.Name,
                x.Name,
                x.AllowedRoles.OrderBy(role => role.Name).Select(role => role.Name).ToList()))
            .ToList();

        return new WorkflowDefinitionDto(workflow.Id, workflow.ProjectId, workflow.Name, workflow.IsDefault, statuses, transitions);
    }

    private static WorkflowStatusOptionDto Map(WorkflowStatus status) =>
        new(status.Id, status.Name, status.Color, status.Category, status.DisplayOrder);

    private static bool CanExecute(WorkflowTransition transition, ISet<string> roleNames)
    {
        if (transition.AllowedRoles.Count == 0)
        {
            return true;
        }

        return transition.AllowedRoles.Any(role => roleNames.Contains(role.Name));
    }

    private static string RequireName(string? value, string message)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? throw new InvalidOperationException(message) : normalized;
    }

    private static string NormalizeColor(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "#42526E" : normalized;
    }

    private static WorkflowDefinition? GetDefaultWorkflow(Project project) =>
        project.WorkflowDefinitions.FirstOrDefault(x => x.IsDefault) ?? project.WorkflowDefinitions.OrderBy(x => x.Id).FirstOrDefault();

    private static void NormalizeOrdering(ICollection<WorkflowStatus> statuses, ICollection<BoardColumn> columns)
    {
        var now = DateTime.UtcNow;
        var statusList = statuses.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToList();
        for (var index = 0; index < statusList.Count; index++)
        {
            statusList[index].DisplayOrder = index + 1;
            statusList[index].UpdatedAtUtc = now;
        }

        var orderedColumns = columns.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToList();
        for (var index = 0; index < orderedColumns.Count; index++)
        {
            orderedColumns[index].DisplayOrder = index + 1;
            orderedColumns[index].UpdatedAtUtc = now;
        }
    }

    private async Task<List<Role>> ResolveAllowedRolesAsync(IReadOnlyCollection<string> allowedRoleNames, CancellationToken cancellationToken)
    {
        var requested = (allowedRoleNames ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requested.Count == 0)
        {
            requested = DefaultEditableRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return (await _users.GetRolesAsync(cancellationToken))
            .Where(x => requested.Contains(x.Name))
            .OrderBy(x => x.Name)
            .ToList();
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

    private static string BuildTransitionMetadata(WorkflowStatus previousStatus, WorkflowStatus currentStatus)
    {
        var metadata = new TransitionMetadata(
            previousStatus.Id,
            previousStatus.Name,
            previousStatus.Category,
            currentStatus.Id,
            currentStatus.Name,
            currentStatus.Category);
        return JsonSerializer.Serialize(metadata);
    }

    private sealed record TransitionMetadata(
        int OldStatusId,
        string OldStatusName,
        StatusCategory OldCategory,
        int NewStatusId,
        string NewStatusName,
        StatusCategory NewCategory);
}



