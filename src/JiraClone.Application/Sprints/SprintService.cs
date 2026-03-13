using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Sprints;

public class SprintService
{
    private readonly ISprintRepository _sprints;
    private readonly IIssueRepository _issues;
    private readonly IAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;

    public SprintService(ISprintRepository sprints, IIssueRepository issues, IAuthorizationService authorization, IUnitOfWork unitOfWork)
    {
        _sprints = sprints;
        _issues = issues;
        _authorization = authorization;
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

        foreach (var issueId in issueIds.Distinct())
        {
            var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
            if (issue is null || issue.ProjectId != sprint.ProjectId)
            {
                continue;
            }

            issue.SprintId = sprintId;
            issue.UpdatedAtUtc = DateTime.UtcNow;
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

        sprint.State = SprintState.Active;
        sprint.StartDate ??= DateOnly.FromDateTime(DateTime.Today);
        sprint.UpdatedAtUtc = DateTime.UtcNow;

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
        }

        sprint.State = SprintState.Closed;
        sprint.EndDate ??= DateOnly.FromDateTime(DateTime.Today);
        sprint.ClosedAtUtc = DateTime.UtcNow;
        sprint.UpdatedAtUtc = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
