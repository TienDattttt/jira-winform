using System.Text.Json;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Sprints;

public class SprintService : ISprintService
{
    private readonly ISprintRepository _sprints;
    private readonly IIssueRepository _issues;
    private readonly IWorkflowRepository _workflows;
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly INotificationRepository _notifications;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SprintService> _logger;

    public SprintService(
        ISprintRepository sprints,
        IIssueRepository issues,
        IWorkflowRepository workflows,
        IProjectRepository projects,
        IUserRepository users,
        INotificationRepository notifications,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork,
        ILogger<SprintService>? logger = null)
    {
        _sprints = sprints;
        _issues = issues;
        _workflows = workflows;
        _projects = projects;
        _users = users;
        _notifications = notifications;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _currentUserContext = currentUserContext;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<SprintService>.Instance;
    }

        public Task<IReadOnlyList<Sprint>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading sprints for project {ProjectId}.", projectId);
        return _sprints.GetByProjectIdAsync(projectId, cancellationToken);
    }

    public Task<Sprint?> GetActiveByProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading active sprint for project {ProjectId}.", projectId);
        return _sprints.GetActiveByProjectIdAsync(projectId, cancellationToken);
    }

    public Task<IReadOnlyList<Issue>> GetAssignableIssuesAsync(int projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading assignable issues for project {ProjectId}.", projectId);
        return _issues.GetProjectIssuesAsync(projectId, cancellationToken);
    }
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

        await QueueSprintNotificationsAsync(sprint, NotificationType.SprintStarted, actorUserId, cancellationToken);
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
        var defaultWorkflow = await _workflows.GetDefaultByProjectAsync(sprint.ProjectId, cancellationToken);
        var backlogStatus = defaultWorkflow?.Statuses
            .Where(x => x.Category == StatusCategory.ToDo)
            .OrderBy(x => x.DisplayOrder)
            .FirstOrDefault();

        foreach (var issue in incompleteIssues)
        {
            issue.SprintId = moveToSprintId;
            if (!moveToSprintId.HasValue)
            {
                if (backlogStatus is null)
                {
                    throw new InvalidOperationException("A To Do status is required to move incomplete sprint issues back to the board.");
                }

                var nextBacklogPosition = await _issues.GetNextBoardPositionAsync(issue.ProjectId, backlogStatus.Id, cancellationToken);
                issue.MoveTo(backlogStatus.Id, nextBacklogPosition);
                issue.WorkflowStatus = backlogStatus;
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
                    NewValue = nextSprint?.Name ?? backlogStatus?.Name ?? "Backlog"
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
                NewValue = nextSprint?.Name ?? backlogStatus?.Name ?? "Backlog"
            }, cancellationToken);
        }

        await QueueSprintNotificationsAsync(sprint, NotificationType.SprintCompleted, actorUserId, cancellationToken);
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

            var (oldCategory, newCategory) = ResolveStatusCategories(activity);
            var movedIntoDone = newCategory == StatusCategory.Done && oldCategory != StatusCategory.Done;
            var movedOutOfDone = oldCategory == StatusCategory.Done && newCategory != StatusCategory.Done;

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

    public async Task<IReadOnlyList<CfdDataPointDto>> GetCfdDataAsync(int projectId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var issues = await _issues.GetProjectIssuesAsync(projectId, cancellationToken);
        var workflow = await _workflows.GetDefaultByProjectAsync(projectId, cancellationToken);
        var statusActivities = await _activityLogs.GetProjectStatusChangesAsync(projectId, cancellationToken);
        if (issues.Count == 0)
        {
            return [];
        }

        var earliestIssueDate = issues.Min(issue => DateOnly.FromDateTime(issue.CreatedAtUtc));
        var earliestActivityDate = statusActivities.Count == 0
            ? earliestIssueDate
            : statusActivities.Min(activity => DateOnly.FromDateTime(activity.OccurredAtUtc));
        var effectiveStart = from == DateTime.MinValue
            ? (earliestIssueDate < earliestActivityDate ? earliestIssueDate : earliestActivityDate)
            : DateOnly.FromDateTime(from);
        var effectiveEnd = to == DateTime.MinValue
            ? DateOnly.FromDateTime(DateTime.Today)
            : DateOnly.FromDateTime(to);

        if (effectiveEnd < effectiveStart)
        {
            (effectiveStart, effectiveEnd) = (effectiveEnd, effectiveStart);
        }

        var statusesById = new Dictionary<int, StatusSnapshot>();
        var statusesByName = new Dictionary<string, StatusSnapshot>(StringComparer.OrdinalIgnoreCase);
        var nextSyntheticId = -1;
        var nextDisplayOrder = 1;

        void RegisterStatus(int? id, string? name, string? color, StatusCategory category, int? displayOrder = null)
        {
            var normalizedName = name?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return;
            }

            if (id.HasValue && statusesById.TryGetValue(id.Value, out var existingById))
            {
                if (!statusesByName.ContainsKey(normalizedName))
                {
                    statusesByName[normalizedName] = existingById;
                }

                return;
            }

            if (statusesByName.TryGetValue(normalizedName, out var existingByName))
            {
                if (id.HasValue && !statusesById.ContainsKey(id.Value))
                {
                    statusesById[id.Value] = existingByName with { Id = id.Value };
                }

                return;
            }

            var resolvedId = id ?? nextSyntheticId--;
            var resolvedOrder = displayOrder ?? nextDisplayOrder++;
            var snapshot = new StatusSnapshot(
                resolvedId,
                normalizedName,
                string.IsNullOrWhiteSpace(color) ? ResolveStatusColor(category) : color!,
                category,
                resolvedOrder);

            statusesById[resolvedId] = snapshot;
            statusesByName[normalizedName] = snapshot;
            nextDisplayOrder = Math.Max(nextDisplayOrder, resolvedOrder + 1);
        }

        foreach (var status in workflow?.Statuses.OrderBy(status => status.DisplayOrder) ?? Enumerable.Empty<WorkflowStatus>())
        {
            RegisterStatus(status.Id, status.Name, status.Color, status.Category, status.DisplayOrder);
        }

        foreach (var issue in issues)
        {
            RegisterStatus(issue.WorkflowStatusId, issue.WorkflowStatus.Name, issue.WorkflowStatus.Color, issue.WorkflowStatus.Category, issue.WorkflowStatus.DisplayOrder);
        }

        var transitionsByIssueId = new Dictionary<int, List<ParsedStatusTransition>>();
        foreach (var activity in statusActivities.Where(activity => activity.IssueId.HasValue).OrderBy(activity => activity.OccurredAtUtc))
        {
            var parsedTransition = ParseStatusTransition(activity, RegisterStatus, statusesById, statusesByName);
            if (parsedTransition is null)
            {
                continue;
            }

            var issueId = activity.IssueId!.Value;
            if (!transitionsByIssueId.TryGetValue(issueId, out var transitions))
            {
                transitions = [];
                transitionsByIssueId[issueId] = transitions;
            }

            transitions.Add(parsedTransition);
        }

        var orderedStatuses = statusesById.Values
            .DistinctBy(status => status.Id)
            .OrderBy(status => status.DisplayOrder)
            .ThenBy(status => status.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var dailyCounts = new Dictionary<(DateOnly Date, int StatusId), int>();

        foreach (var issue in issues)
        {
            var issueStartDate = DateOnly.FromDateTime(issue.CreatedAtUtc);
            if (issueStartDate > effectiveEnd)
            {
                continue;
            }

            var startDate = issueStartDate > effectiveStart ? issueStartDate : effectiveStart;
            var issueTransitions = transitionsByIssueId.GetValueOrDefault(issue.Id, []);
            var currentStatus = issueTransitions.FirstOrDefault()?.OldStatus
                ?? statusesById.GetValueOrDefault(issue.WorkflowStatusId)
                ?? statusesByName.GetValueOrDefault(issue.WorkflowStatus.Name)
                ?? new StatusSnapshot(issue.WorkflowStatusId, issue.WorkflowStatus.Name, issue.WorkflowStatus.Color, issue.WorkflowStatus.Category, issue.WorkflowStatus.DisplayOrder);
            var transitionIndex = 0;

            while (transitionIndex < issueTransitions.Count && DateOnly.FromDateTime(issueTransitions[transitionIndex].OccurredAtUtc) < startDate)
            {
                currentStatus = issueTransitions[transitionIndex].NewStatus;
                transitionIndex++;
            }

            for (var day = startDate; day <= effectiveEnd; day = day.AddDays(1))
            {
                while (transitionIndex < issueTransitions.Count && DateOnly.FromDateTime(issueTransitions[transitionIndex].OccurredAtUtc) <= day)
                {
                    currentStatus = issueTransitions[transitionIndex].NewStatus;
                    transitionIndex++;
                }

                var key = (day, currentStatus.Id);
                dailyCounts[key] = dailyCounts.TryGetValue(key, out var count) ? count + 1 : 1;
            }
        }

        var result = new List<CfdDataPointDto>();
        foreach (var day in EnumerateDays(effectiveStart, effectiveEnd))
        {
            foreach (var status in orderedStatuses)
            {
                result.Add(new CfdDataPointDto(
                    day,
                    status.Id,
                    status.Name,
                    status.Color,
                    status.Category,
                    status.DisplayOrder,
                    dailyCounts.GetValueOrDefault((day, status.Id), 0)));
            }
        }

        return result;
    }

    public async Task<SprintReportDto?> GetSprintReportAsync(int sprintId, CancellationToken cancellationToken = default)
    {
        var sprint = await _sprints.GetByIdAsync(sprintId, cancellationToken);
        if (sprint is null || sprint.State != SprintState.Closed)
        {
            return null;
        }

        var issues = await _issues.GetProjectIssuesAsync(sprint.ProjectId, cancellationToken);
        var activities = await _activityLogs.GetProjectActivityAsync(sprint.ProjectId, int.MaxValue, cancellationToken);
        var activitiesByIssueId = activities
            .Where(activity => activity.IssueId.HasValue)
            .GroupBy(activity => activity.IssueId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ActivityLogEntity>)group.OrderBy(activity => activity.OccurredAtUtc).ToList());

        var (startDate, _) = ResolveSprintWindow(sprint);
        var closeDate = ResolveSprintCloseDate(sprint);
        var committedIssues = issues
            .Where(issue => IsInSprintAtDate(issue, sprint, startDate, activitiesByIssueId.GetValueOrDefault(issue.Id, Array.Empty<ActivityLogEntity>())))
            .OrderBy(issue => issue.IssueKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var removedIssueIds = committedIssues
            .Where(issue => WasRemovedFromSprintMidSprint(issue, sprint, startDate, closeDate, activitiesByIssueId.GetValueOrDefault(issue.Id, Array.Empty<ActivityLogEntity>())))
            .Select(issue => issue.Id)
            .ToHashSet();

        var completedWork = committedIssues
            .Where(issue => !removedIssueIds.Contains(issue.Id) && IsDoneByDate(issue, sprint, closeDate, activitiesByIssueId.GetValueOrDefault(issue.Id, Array.Empty<ActivityLogEntity>())))
            .Select(issue => MapSprintReportIssue(issue, ResolveStatusAtDate(issue, closeDate, activitiesByIssueId.GetValueOrDefault(issue.Id, Array.Empty<ActivityLogEntity>()))))
            .ToList();
        var notCompleted = committedIssues
            .Where(issue => !removedIssueIds.Contains(issue.Id) && !IsDoneByDate(issue, sprint, closeDate, activitiesByIssueId.GetValueOrDefault(issue.Id, Array.Empty<ActivityLogEntity>())))
            .Select(issue => MapSprintReportIssue(issue, ResolveStatusAtDate(issue, closeDate, activitiesByIssueId.GetValueOrDefault(issue.Id, Array.Empty<ActivityLogEntity>()))))
            .ToList();
        var removedFromSprint = committedIssues
            .Where(issue => removedIssueIds.Contains(issue.Id))
            .Select(issue => MapSprintReportIssue(issue, ResolveStatusAtDate(issue, closeDate, activitiesByIssueId.GetValueOrDefault(issue.Id, Array.Empty<ActivityLogEntity>()))))
            .ToList();

        var committedStoryPoints = committedIssues.Sum(issue => issue.StoryPoints ?? 0);
        var completedStoryPoints = completedWork.Sum(issue => issue.StoryPoints);
        var completionPercentage = committedStoryPoints == 0
            ? (completedWork.Count == 0 ? 0d : 100d)
            : Math.Round(completedStoryPoints * 100d / committedStoryPoints, 1);

        return new SprintReportDto(
            sprint.Id,
            sprint.Name,
            startDate,
            closeDate,
            sprint.ClosedAtUtc,
            completedWork,
            notCompleted,
            removedFromSprint,
            committedStoryPoints,
            completedStoryPoints,
            completionPercentage);
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

    private static ParsedStatusTransition? ParseStatusTransition(
        ActivityLogEntity activity,
        Action<int?, string?, string?, StatusCategory, int?> registerStatus,
        IReadOnlyDictionary<int, StatusSnapshot> statusesById,
        IReadOnlyDictionary<string, StatusSnapshot> statusesByName)
    {
        var metadata = TryParseTransitionMetadata(activity.MetadataJson);
        if (metadata is not null)
        {
            registerStatus(metadata.OldStatusId, metadata.OldStatusName, null, metadata.OldCategory, null);
            registerStatus(metadata.NewStatusId, metadata.NewStatusName, null, metadata.NewCategory, null);

            var oldStatus = statusesById.GetValueOrDefault(metadata.OldStatusId)
                ?? statusesByName.GetValueOrDefault(metadata.OldStatusName);
            var newStatus = statusesById.GetValueOrDefault(metadata.NewStatusId)
                ?? statusesByName.GetValueOrDefault(metadata.NewStatusName);
            if (oldStatus is not null && newStatus is not null)
            {
                return new ParsedStatusTransition(activity.OccurredAtUtc, oldStatus, newStatus);
            }
        }

        var oldName = activity.OldValue?.Trim();
        var newName = activity.NewValue?.Trim();
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
        {
            return null;
        }

        var (oldCategory, newCategory) = ResolveStatusCategories(activity);
        registerStatus(null, oldName, null, oldCategory, null);
        registerStatus(null, newName, null, newCategory, null);
        return new ParsedStatusTransition(
            activity.OccurredAtUtc,
            statusesByName[oldName],
            statusesByName[newName]);
    }

    private static StatusSnapshot ResolveStatusAtDate(Issue issue, DateOnly targetDate, IReadOnlyList<ActivityLogEntity> issueActivities)
    {
        var statusChanges = issueActivities
            .Where(activity => activity.ActionType == ActivityActionType.StatusChanged)
            .OrderBy(activity => activity.OccurredAtUtc)
            .ToList();
        var latestStatusChange = statusChanges.LastOrDefault(activity => DateOnly.FromDateTime(activity.OccurredAtUtc) <= targetDate);
        if (latestStatusChange is not null)
        {
            var metadata = TryParseTransitionMetadata(latestStatusChange.MetadataJson);
            if (metadata is not null)
            {
                return new StatusSnapshot(
                    metadata.NewStatusId,
                    metadata.NewStatusName,
                    ResolveStatusColor(metadata.NewCategory),
                    metadata.NewCategory,
                    metadata.NewStatusId);
            }

            var (_, newCategory) = ResolveStatusCategories(latestStatusChange);
            return new StatusSnapshot(
                issue.WorkflowStatusId,
                latestStatusChange.NewValue ?? issue.WorkflowStatus.Name,
                ResolveStatusColor(newCategory),
                newCategory,
                issue.WorkflowStatus.DisplayOrder);
        }

        var firstStatusChange = statusChanges.FirstOrDefault();
        if (firstStatusChange is not null)
        {
            var metadata = TryParseTransitionMetadata(firstStatusChange.MetadataJson);
            if (metadata is not null)
            {
                return new StatusSnapshot(
                    metadata.OldStatusId,
                    metadata.OldStatusName,
                    ResolveStatusColor(metadata.OldCategory),
                    metadata.OldCategory,
                    metadata.OldStatusId);
            }

            var (oldCategory, _) = ResolveStatusCategories(firstStatusChange);
            return new StatusSnapshot(
                issue.WorkflowStatusId,
                firstStatusChange.OldValue ?? issue.WorkflowStatus.Name,
                ResolveStatusColor(oldCategory),
                oldCategory,
                issue.WorkflowStatus.DisplayOrder);
        }

        return new StatusSnapshot(
            issue.WorkflowStatusId,
            issue.WorkflowStatus.Name,
            string.IsNullOrWhiteSpace(issue.WorkflowStatus.Color) ? ResolveStatusColor(issue.WorkflowStatus.Category) : issue.WorkflowStatus.Color,
            issue.WorkflowStatus.Category,
            issue.WorkflowStatus.DisplayOrder);
    }

    private static bool IsDoneByDate(
        Issue issue,
        Sprint sprint,
        DateOnly targetDate,
        IReadOnlyList<ActivityLogEntity> issueActivities)
    {
        var movedOutBeforeTarget = issueActivities.Any(activity =>
            activity.ActionType == ActivityActionType.SprintAssigned
            && DateOnly.FromDateTime(activity.OccurredAtUtc) <= targetDate
            && string.Equals(activity.OldValue, sprint.Name, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(activity.NewValue, sprint.Name, StringComparison.OrdinalIgnoreCase));
        if (movedOutBeforeTarget)
        {
            return false;
        }

        var statusChanges = issueActivities
            .Where(activity => activity.ActionType == ActivityActionType.StatusChanged)
            .OrderBy(activity => activity.OccurredAtUtc)
            .ToList();
        var latestStatusChange = statusChanges.LastOrDefault(activity => DateOnly.FromDateTime(activity.OccurredAtUtc) <= targetDate);
        if (latestStatusChange is not null)
        {
            return ResolveStatusCategories(latestStatusChange).NewCategory == StatusCategory.Done;
        }

        var firstStatusChange = statusChanges.FirstOrDefault();
        if (firstStatusChange is not null)
        {
            return ResolveStatusCategories(firstStatusChange).OldCategory == StatusCategory.Done;
        }

        return issue.WorkflowStatus.Category == StatusCategory.Done && issue.SprintId == sprint.Id;
    }

    private static bool IsInSprintAtDate(
        Issue issue,
        Sprint sprint,
        DateOnly targetDate,
        IReadOnlyList<ActivityLogEntity> issueActivities)
    {
        var sprintChanges = issueActivities
            .Where(activity => activity.ActionType == ActivityActionType.SprintAssigned)
            .OrderBy(activity => activity.OccurredAtUtc)
            .ToList();
        var latestSprintChange = sprintChanges.LastOrDefault(activity => DateOnly.FromDateTime(activity.OccurredAtUtc) <= targetDate);
        if (latestSprintChange is not null)
        {
            return string.Equals(latestSprintChange.NewValue, sprint.Name, StringComparison.OrdinalIgnoreCase);
        }

        var firstSprintChange = sprintChanges.FirstOrDefault();
        if (firstSprintChange is not null)
        {
            return string.Equals(firstSprintChange.OldValue, sprint.Name, StringComparison.OrdinalIgnoreCase);
        }

        return issue.SprintId == sprint.Id;
    }

    private static bool WasRemovedFromSprintMidSprint(
        Issue issue,
        Sprint sprint,
        DateOnly startDate,
        DateOnly closeDate,
        IReadOnlyList<ActivityLogEntity> issueActivities)
    {
        var evaluationEndDate = closeDate.AddDays(-1);
        if (evaluationEndDate < startDate)
        {
            return false;
        }

        var removedDuringSprint = issueActivities.Any(activity =>
            activity.ActionType == ActivityActionType.SprintAssigned
            && DateOnly.FromDateTime(activity.OccurredAtUtc) >= startDate
            && DateOnly.FromDateTime(activity.OccurredAtUtc) <= evaluationEndDate
            && string.Equals(activity.OldValue, sprint.Name, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(activity.NewValue, sprint.Name, StringComparison.OrdinalIgnoreCase));
        if (!removedDuringSprint)
        {
            return false;
        }

        return !IsInSprintAtDate(issue, sprint, evaluationEndDate, issueActivities);
    }

    private static SprintReportIssueDto MapSprintReportIssue(Issue issue, StatusSnapshot status)
    {
        var assigneeSummary = issue.Assignees.Count == 0
            ? "Unassigned"
            : string.Join(", ", issue.Assignees.Select(assignee => assignee.User.DisplayName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));

        return new SprintReportIssueDto(
            issue.Id,
            issue.IssueKey,
            issue.Title,
            issue.Type,
            status.Name,
            status.Category,
            issue.StoryPoints ?? 0,
            assigneeSummary);
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

    private static (StatusCategory OldCategory, StatusCategory NewCategory) ResolveStatusCategories(ActivityLogEntity activity)
    {
        var metadata = TryParseTransitionMetadata(activity.MetadataJson);
        if (metadata is not null)
        {
            return (metadata.OldCategory, metadata.NewCategory);
        }

        var oldCategory = string.Equals(activity.OldValue, "Done", StringComparison.OrdinalIgnoreCase)
            ? StatusCategory.Done
            : string.Equals(activity.OldValue, "In Progress", StringComparison.OrdinalIgnoreCase)
                ? StatusCategory.InProgress
                : StatusCategory.ToDo;
        var newCategory = string.Equals(activity.NewValue, "Done", StringComparison.OrdinalIgnoreCase)
            ? StatusCategory.Done
            : string.Equals(activity.NewValue, "In Progress", StringComparison.OrdinalIgnoreCase)
                ? StatusCategory.InProgress
                : StatusCategory.ToDo;
        return (oldCategory, newCategory);
    }

    private static TransitionMetadata? TryParseTransitionMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TransitionMetadata>(metadataJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ResolveStatusColor(StatusCategory category) => category switch
    {
        StatusCategory.Done => "#36B37E",
        StatusCategory.InProgress => "#0052CC",
        _ => "#6B778C"
    };

    private async Task QueueSprintNotificationsAsync(Sprint sprint, NotificationType notificationType, int? actorUserId, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(sprint.ProjectId, cancellationToken);
        var actor = actorUserId.HasValue ? await _users.GetByIdAsync(actorUserId.Value, cancellationToken) : null;
        var actorName = actor?.DisplayName ?? "Someone";
        var title = notificationType == NotificationType.SprintStarted
            ? $"Sprint started: {sprint.Name}"
            : $"Sprint completed: {sprint.Name}";
        var body = notificationType == NotificationType.SprintStarted
            ? $"{actorName} started sprint {sprint.Name} in {project?.Name ?? "this project"}."
            : $"{actorName} completed sprint {sprint.Name} in {project?.Name ?? "this project"}.";

        foreach (var member in await _users.GetProjectUsersAsync(sprint.ProjectId, cancellationToken))
        {
            await _notifications.AddAsync(new Notification
            {
                RecipientUserId = member.Id,
                ProjectId = sprint.ProjectId,
                Type = notificationType,
                Title = title,
                Body = body,
                IsRead = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }, cancellationToken);
        }
    }

    private int? GetActorUserId() => _currentUserContext.CurrentUser?.Id;

    private sealed record StatusSnapshot(
        int Id,
        string Name,
        string Color,
        StatusCategory Category,
        int DisplayOrder);

    private sealed record ParsedStatusTransition(
        DateTime OccurredAtUtc,
        StatusSnapshot OldStatus,
        StatusSnapshot NewStatus);

    private sealed record TransitionMetadata(
        int OldStatusId,
        string OldStatusName,
        StatusCategory OldCategory,
        int NewStatusId,
        string NewStatusName,
        StatusCategory NewCategory);
}







