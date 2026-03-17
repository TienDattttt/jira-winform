using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Dashboard;

public class DashboardQueryService
{
    private readonly IBoardQueryService _boardQueries;
    private readonly IIssueQueryService _issueQueries;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IUserRepository _users;
    private readonly ISprintRepository _sprints;
    private readonly ICurrentUserContext _currentUserContext;

    public DashboardQueryService(
        IBoardQueryService boardQueries,
        IIssueQueryService issueQueries,
        IActivityLogRepository activityLogs,
        IUserRepository users,
        ISprintRepository sprints,
        ICurrentUserContext currentUserContext)
    {
        _boardQueries = boardQueries;
        _issueQueries = issueQueries;
        _activityLogs = activityLogs;
        _users = users;
        _sprints = sprints;
        _currentUserContext = currentUserContext;
    }

    public async Task<DashboardOverviewDto> GetOverviewAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var activeSprint = await _sprints.GetActiveByProjectIdAsync(projectId, cancellationToken);
        var sprintBoard = activeSprint is null
            ? BuildEmptyBoard()
            : await _boardQueries.GetBoardAsync(projectId, activeSprint.Id, cancellationToken);
        var projectIssues = await _issueQueries.GetProjectIssuesAsync(projectId, cancellationToken);
        var recentActivities = await _activityLogs.GetProjectActivityAsync(projectId, 10, cancellationToken);
        var projectUsers = await _users.GetProjectUsersAsync(projectId, cancellationToken);
        var currentUserId = _currentUserContext.CurrentUser?.Id;

        return new DashboardOverviewDto(
            BuildSprintProgress(activeSprint, sprintBoard),
            BuildIssueStatistics(projectIssues),
            BuildRecentActivities(recentActivities),
            BuildAssignedToMe(projectIssues, currentUserId),
            BuildTeamWorkload(projectUsers, projectIssues));
    }

    private static DashboardSprintProgressDto BuildSprintProgress(Sprint? activeSprint, IReadOnlyList<BoardColumnDto> board)
    {
        var sprintIssues = board.SelectMany(column => column.Issues).ToList();
        var doneIssues = sprintIssues.Count(issue => issue.Status == IssueStatus.Done);
        var totalIssues = sprintIssues.Count;
        var doneStoryPoints = sprintIssues.Where(issue => issue.Status == IssueStatus.Done).Sum(issue => issue.StoryPoints ?? 0);
        var totalStoryPoints = sprintIssues.Sum(issue => issue.StoryPoints ?? 0);
        var counts = Enum.GetValues<IssueStatus>()
            .Select(status => new DashboardStatusCountDto(status, sprintIssues.Count(issue => issue.Status == status)))
            .ToList();

        return new DashboardSprintProgressDto(
            activeSprint is not null,
            activeSprint?.Name,
            activeSprint?.StartDate,
            activeSprint?.EndDate,
            doneIssues,
            totalIssues,
            doneStoryPoints,
            totalStoryPoints,
            counts);
    }

    private static DashboardIssueStatisticsDto BuildIssueStatistics(IReadOnlyList<DashboardIssueDto> issues)
    {
        return new DashboardIssueStatisticsDto(
            BuildChartSlices(issues.GroupBy(issue => issue.Type).Select(group => (Label: FormatIssueType(group.Key), Value: group.Count()))),
            BuildChartSlices(issues.GroupBy(issue => issue.Priority).Select(group => (Label: FormatPriority(group.Key), Value: group.Count()))));
    }

    private static IReadOnlyList<DashboardChartSliceDto> BuildChartSlices(IEnumerable<(string Label, int Value)> groups) =>
        groups
            .Where(group => group.Value > 0)
            .OrderByDescending(group => group.Value)
            .ThenBy(group => group.Label)
            .Select(group => new DashboardChartSliceDto(group.Label, group.Value))
            .ToList();

    private static IReadOnlyList<DashboardActivityDto> BuildRecentActivities(IEnumerable<JiraClone.Domain.Entities.ActivityLog> activities) =>
        activities
            .OrderByDescending(activity => activity.OccurredAtUtc)
            .Take(10)
            .Select(activity => new DashboardActivityDto(
                activity.Id,
                activity.IssueId,
                activity.Issue?.IssueKey,
                activity.User.DisplayName,
                activity.User.AvatarPath,
                activity.ActionType,
                activity.FieldName,
                activity.OldValue,
                activity.NewValue,
                activity.OccurredAtUtc))
            .ToList();

    private static IReadOnlyList<DashboardIssueDto> BuildAssignedToMe(IEnumerable<DashboardIssueDto> issues, int? currentUserId) =>
        issues
            .Where(issue => issue.Status == IssueStatus.InProgress && currentUserId.HasValue && issue.Assignees.Any(assignee => assignee.UserId == currentUserId.Value))
            .OrderByDescending(issue => issue.Priority)
            .ThenBy(issue => issue.IssueKey)
            .ToList();

    private static IReadOnlyList<DashboardTeamWorkloadDto> BuildTeamWorkload(IEnumerable<User> users, IEnumerable<DashboardIssueDto> issues)
    {
        var issueList = issues.ToList();
        return users
            .Select(user =>
            {
                var assigned = issueList.Where(issue => issue.Assignees.Any(assignee => assignee.UserId == user.Id)).ToList();
                var openIssues = assigned.Count(issue => issue.Status is IssueStatus.Backlog or IssueStatus.Selected);
                var inProgressIssues = assigned.Count(issue => issue.Status == IssueStatus.InProgress);
                return new DashboardTeamWorkloadDto(user.Id, user.DisplayName, user.AvatarPath, openIssues, inProgressIssues);
            })
            .OrderByDescending(item => item.InProgressIssues)
            .ThenByDescending(item => item.OpenIssues)
            .ThenBy(item => item.DisplayName)
            .ToList();
    }

    private static IReadOnlyList<BoardColumnDto> BuildEmptyBoard() =>
        new[]
        {
            new BoardColumnDto(IssueStatus.Backlog, "Backlog", Array.Empty<IssueSummaryDto>()),
            new BoardColumnDto(IssueStatus.Selected, "Selected", Array.Empty<IssueSummaryDto>()),
            new BoardColumnDto(IssueStatus.InProgress, "In Progress", Array.Empty<IssueSummaryDto>()),
            new BoardColumnDto(IssueStatus.Done, "Done", Array.Empty<IssueSummaryDto>())
        };

    private static string FormatIssueType(IssueType type) => type switch
    {
        IssueType.Subtask => "Subtask",
        _ => type.ToString()
    };

    private static string FormatPriority(IssuePriority priority) => priority.ToString();
}
