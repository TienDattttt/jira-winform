using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Application.Sprints;

public class SprintService : ISprintService
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

    public async Task<BurndownReportDto?> GetBurndownDataAsync(int sprintId, CancellationToken cancellationToken = default)
    {
        var sprint = await _sprints.GetByIdAsync(sprintId, cancellationToken);
        if (sprint is null)
        {
            return null;
        }

        var issues = await _issues.GetProjectIssuesAsync(sprint.ProjectId, cancellationToken);
        var activities = await _activityLogs.GetProjectActivityAsync(sprint.ProjectId, int.MaxValue, cancellationToken);
        var sprintIssues = BuildSprintIssueSet(sprint, issues, activities);
        var storyPointsByIssueId = sprintIssues.ToDictionary(x => x.Id, x => x.StoryPoints ?? 0);
        var totalStoryPoints = storyPointsByIssueId.Values.Sum();
        var (startDate, endDate) = ResolveSprintWindow(sprint);
        var days = EnumerateDays(startDate, endDate).ToList();
        var dailyDelta = days.ToDictionary(x => x, _ => 0);

        foreach (var activity in activities
            .Where(x => x.IssueId.HasValue
                && storyPointsByIssueId.ContainsKey(x.IssueId.Value)
                && x.ActionType == ActivityActionType.StatusChanged)
            .OrderBy(x => x.OccurredAtUtc))
        {
            var day = DateOnly.FromDateTime(activity.OccurredAtUtc);
            if (!dailyDelta.ContainsKey(day))
            {
                continue;
            }

            var storyPoints = storyPointsByIssueId[activity.IssueId!.Value];
            if (storyPoints == 0)
            {
                continue;
            }

            var movedIntoDone = string.Equals(activity.NewValue, IssueStatus.Done.ToString(), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(activity.OldValue, IssueStatus.Done.ToString(), StringComparison.OrdinalIgnoreCase);
            var movedOutOfDone = string.Equals(activity.OldValue, IssueStatus.Done.ToString(), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(activity.NewValue, IssueStatus.Done.ToString(), StringComparison.OrdinalIgnoreCase);

            if (movedIntoDone)
            {
                dailyDelta[day] -= storyPoints;
            }
            else if (movedOutOfDone)
            {
                dailyDelta[day] += storyPoints;
            }
        }

        var idealPoints = new List<BurndownPointDto>(days.Count);
        var actualPoints = new List<BurndownPointDto>(days.Count);
        var remainingStoryPoints = totalStoryPoints;

        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            remainingStoryPoints = Math.Clamp(remainingStoryPoints + dailyDelta[day], 0, totalStoryPoints);
            actualPoints.Add(new BurndownPointDto(day, index + 1, remainingStoryPoints));

            var idealRemaining = days.Count == 1
                ? 0d
                : totalStoryPoints * (1d - (index / (double)(days.Count - 1)));
            idealPoints.Add(new BurndownPointDto(day, index + 1, Math.Max(0d, Math.Round(idealRemaining, 2))));
        }

        return new BurndownReportDto(
            sprint.Id,
            sprint.Name,
            startDate,
            endDate,
            totalStoryPoints,
            idealPoints,
            actualPoints);
    }

    public async Task<VelocityReportDto> GetVelocityDataAsync(int projectId, int lastN = 6, CancellationToken cancellationToken = default)
    {
        var sprintCount = Math.Max(1, lastN);
        var sprints = (await _sprints.GetByProjectIdAsync(projectId, cancellationToken))
            .Where(x => x.State == SprintState.Closed)
            .OrderByDescending(GetSprintSortDate)
            .Take(sprintCount)
            .OrderBy(GetSprintSortDate)
            .ToList();

        if (sprints.Count == 0)
        {
            return new VelocityReportDto(projectId, Array.Empty<VelocitySprintDto>(), 0d);
        }

        var issues = await _issues.GetProjectIssuesAsync(projectId, cancellationToken);
        var activities = await _activityLogs.GetProjectActivityAsync(projectId, int.MaxValue, cancellationToken);
        var activitiesByIssueId = activities
            .Where(x => x.IssueId.HasValue)
            .GroupBy(x => x.IssueId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ActivityLogEntity>)group.OrderBy(x => x.OccurredAtUtc).ToList());

        var velocitySprints = new List<VelocitySprintDto>(sprints.Count);
        foreach (var sprint in sprints)
        {
            var sprintIssues = BuildSprintIssueSet(sprint, issues, activities);
            var committedStoryPoints = sprintIssues.Sum(x => x.StoryPoints ?? 0);
            var closeDate = ResolveSprintCloseDate(sprint);
            var completedStoryPoints = sprintIssues
                .Where(issue => IsDoneByDate(
                    issue,
                    sprint,
                    closeDate,
                    activitiesByIssueId.GetValueOrDefault(issue.Id, Array.Empty<ActivityLogEntity>())))
                .Sum(x => x.StoryPoints ?? 0);

            velocitySprints.Add(new VelocitySprintDto(
                sprint.Id,
                sprint.Name,
                sprint.StartDate,
                sprint.EndDate,
                committedStoryPoints,
                completedStoryPoints));
        }

        var averageCompletedStoryPoints = velocitySprints.Count == 0
            ? 0d
            : Math.Round(velocitySprints.Average(x => x.CompletedStoryPoints), 2);

        return new VelocityReportDto(projectId, velocitySprints, averageCompletedStoryPoints);
    }

    private static IReadOnlyList<Issue> BuildSprintIssueSet(
        Sprint sprint,
        IReadOnlyList<Issue> projectIssues,
        IReadOnlyList<ActivityLogEntity> activities)
    {
        var sprintIssueIds = new HashSet<int>(projectIssues.Where(x => x.SprintId == sprint.Id).Select(x => x.Id));

        foreach (var activity in activities.Where(x => x.ActionType == ActivityActionType.SprintAssigned && x.IssueId.HasValue))
        {
            if (string.Equals(activity.OldValue, sprint.Name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(activity.NewValue, sprint.Name, StringComparison.OrdinalIgnoreCase))
            {
                sprintIssueIds.Add(activity.IssueId!.Value);
            }
        }

        return projectIssues.Where(x => sprintIssueIds.Contains(x.Id)).ToList();
    }

    private static bool IsDoneByDate(
        Issue issue,
        Sprint sprint,
        DateOnly targetDate,
        IReadOnlyList<ActivityLogEntity> issueActivities)
    {
        var latestStatusChange = issueActivities
            .Where(x => x.ActionType == ActivityActionType.StatusChanged && DateOnly.FromDateTime(x.OccurredAtUtc) <= targetDate)
            .OrderByDescending(x => x.OccurredAtUtc)
            .FirstOrDefault();

        if (latestStatusChange is not null)
        {
            return string.Equals(latestStatusChange.NewValue, IssueStatus.Done.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        var movedOutBeforeClose = issueActivities.Any(x =>
            x.ActionType == ActivityActionType.SprintAssigned
            && DateOnly.FromDateTime(x.OccurredAtUtc) <= targetDate
            && string.Equals(x.OldValue, sprint.Name, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(x.NewValue, sprint.Name, StringComparison.OrdinalIgnoreCase));

        if (movedOutBeforeClose)
        {
            return false;
        }

        return issue.SprintId == sprint.Id && issue.Status == IssueStatus.Done;
    }

    private static IEnumerable<DateOnly> EnumerateDays(DateOnly startDate, DateOnly endDate)
    {
        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    private static (DateOnly StartDate, DateOnly EndDate) ResolveSprintWindow(Sprint sprint)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var startDate = sprint.StartDate ?? sprint.EndDate ?? today;
        var endDate = sprint.EndDate ?? (sprint.State == SprintState.Active ? today : startDate.AddDays(13));

        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        return (startDate, endDate);
    }

    private static DateOnly ResolveSprintCloseDate(Sprint sprint)
    {
        if (sprint.ClosedAtUtc.HasValue)
        {
            return DateOnly.FromDateTime(sprint.ClosedAtUtc.Value);
        }

        var (_, endDate) = ResolveSprintWindow(sprint);
        return endDate;
    }

    private static DateTime GetSprintSortDate(Sprint sprint)
    {
        if (sprint.ClosedAtUtc.HasValue)
        {
            return sprint.ClosedAtUtc.Value;
        }

        if (sprint.EndDate.HasValue)
        {
            return sprint.EndDate.Value.ToDateTime(TimeOnly.MinValue);
        }

        if (sprint.StartDate.HasValue)
        {
            return sprint.StartDate.Value.ToDateTime(TimeOnly.MinValue);
        }

        return DateTime.MinValue;
    }

    private int? GetActorUserId() => _currentUserContext.CurrentUser?.Id;
}
