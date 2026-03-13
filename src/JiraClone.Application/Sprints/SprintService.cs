using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Application.Sprints;

public class SprintService
{
    private readonly ISprintRepository _sprints;
    private readonly IIssueRepository _issues;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;

    public SprintService(
        ISprintRepository sprints,
        IIssueRepository issues,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork)
    {
        _sprints = sprints;
        _issues = issues;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _currentUserContext = currentUserContext;
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<Sprint>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default) =>
        _sprints.GetByProjectIdAsync(projectId, cancellationToken);

    public Task<Sprint?> GetActiveByProjectAsync(int projectId, CancellationToken cancellationToken = default) =>
        _sprints.GetActiveByProjectIdAsync(projectId, cancellationToken);

    public Task<IReadOnlyList<Issue>> GetAssignableIssuesAsync(int projectId, CancellationToken cancellationToken = default) =>
        _issues.GetProjectIssuesAsync(projectId, cancellationToken);

    public async Task<Sprint> CreateAsync(int projectId, string name, string? goal, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager);
        var sprint = new Sprint
        {
            ProjectId = projectId,
            Name = name.Trim(),
            Goal = goal,
            StartDate = startDate,
            EndDate = endDate,
            State = SprintState.Planned
        };

        await _sprints.AddAsync(sprint, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return sprint;
    }

    public async Task<bool> AssignIssuesAsync(int sprintId, IReadOnlyCollection<int> issueIds, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager);
        var sprint = await _sprints.GetByIdAsync(sprintId, cancellationToken);
        if (sprint is null || issueIds.Count == 0)
        {
            return false;
        }

        var actorUserId = GetActorUserId();
        foreach (var issueId in issueIds.Distinct())
        {
            var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
            if (issue is null || issue.ProjectId != sprint.ProjectId)
            {
                continue;
            }

            issue.SprintId = sprintId;
            issue.UpdatedAtUtc = DateTime.UtcNow;

            if (actorUserId.HasValue)
            {
                await _activityLogs.AddAsync(new ActivityLogEntity
                {
                    ProjectId = sprint.ProjectId,
                    IssueId = issue.Id,
                    UserId = actorUserId.Value,
                    ActionType = ActivityActionType.SprintAssigned,
                    NewValue = sprint.Name
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> StartSprintAsync(int sprintId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager);
        var sprint = await _sprints.GetByIdAsync(sprintId, cancellationToken);
        if (sprint is null || sprint.State == SprintState.Active)
        {
            return false;
        }

        var activeSprint = await _sprints.GetActiveByProjectIdAsync(sprint.ProjectId, cancellationToken);
        if (activeSprint is not null && activeSprint.Id != sprintId)
        {
            throw new InvalidOperationException("Another sprint is already active for this project.");
        }

        var previousState = sprint.State;
        sprint.State = SprintState.Active;
        sprint.StartDate ??= DateOnly.FromDateTime(DateTime.Today);
        sprint.UpdatedAtUtc = DateTime.UtcNow;

        var actorUserId = GetActorUserId();
        if (actorUserId.HasValue)
        {
            await _activityLogs.AddAsync(new ActivityLogEntity
            {
                ProjectId = sprint.ProjectId,
                UserId = actorUserId.Value,
                ActionType = ActivityActionType.Updated,
                FieldName = nameof(Sprint.State),
                OldValue = previousState.ToString(),
                NewValue = sprint.State.ToString()
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CloseSprintAsync(int sprintId, int? moveToSprintId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager);
        var sprint = await _sprints.GetByIdAsync(sprintId, cancellationToken);
        if (sprint is null || sprint.State != SprintState.Active)
        {
            return false;
        }

        if (moveToSprintId.HasValue && moveToSprintId.Value == sprintId)
        {
            throw new InvalidOperationException("Cannot move incomplete issues to the same sprint.");
        }

        Sprint? nextSprint = null;
        if (moveToSprintId.HasValue)
        {
            nextSprint = await _sprints.GetByIdAsync(moveToSprintId.Value, cancellationToken);
            if (nextSprint is null || nextSprint.ProjectId != sprint.ProjectId)
            {
                throw new InvalidOperationException("Target sprint was not found in this project.");
            }
        }

        var actorUserId = GetActorUserId();
        var incompleteIssues = await _issues.GetIncompleteBySprintIdAsync(sprintId, cancellationToken);
        foreach (var issue in incompleteIssues)
        {
            issue.SprintId = moveToSprintId;
            if (!moveToSprintId.HasValue)
            {
                var nextBacklogPosition = await _issues.GetNextBoardPositionAsync(issue.ProjectId, IssueStatus.Backlog, cancellationToken);
                issue.MoveTo(IssueStatus.Backlog, nextBacklogPosition);
            }
            else
            {
                issue.UpdatedAtUtc = DateTime.UtcNow;
            }

            if (actorUserId.HasValue)
            {
                await _activityLogs.AddAsync(new ActivityLogEntity
                {
                    ProjectId = sprint.ProjectId,
                    IssueId = issue.Id,
                    UserId = actorUserId.Value,
                    ActionType = ActivityActionType.SprintAssigned,
                    OldValue = sprint.Name,
                    NewValue = nextSprint?.Name ?? "Backlog"
                }, cancellationToken);
            }
        }

        sprint.State = SprintState.Closed;
        sprint.EndDate ??= DateOnly.FromDateTime(DateTime.Today);
        sprint.ClosedAtUtc = DateTime.UtcNow;
        sprint.UpdatedAtUtc = DateTime.UtcNow;

        if (actorUserId.HasValue)
        {
            await _activityLogs.AddAsync(new ActivityLogEntity
            {
                ProjectId = sprint.ProjectId,
                UserId = actorUserId.Value,
                ActionType = ActivityActionType.SprintClosed,
                OldValue = sprint.Name,
                NewValue = nextSprint?.Name ?? "Backlog"
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private int? GetActorUserId() => _currentUserContext.CurrentUser?.Id;
}
