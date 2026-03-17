using JiraClone.Domain.Enums;

namespace JiraClone.Application.Models;

public sealed record DashboardOverviewDto(
    DashboardSprintProgressDto SprintProgress,
    DashboardIssueStatisticsDto IssueStatistics,
    IReadOnlyList<DashboardActivityDto> RecentActivities,
    IReadOnlyList<DashboardIssueDto> AssignedToMe,
    IReadOnlyList<DashboardTeamWorkloadDto> TeamWorkload);

public sealed record DashboardSprintProgressDto(
    bool HasActiveSprint,
    string? SprintName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int DoneIssues,
    int TotalIssues,
    int DoneStoryPoints,
    int TotalStoryPoints,
    IReadOnlyList<DashboardStatusCountDto> StatusCounts);

public sealed record DashboardStatusCountDto(IssueStatus Status, int Count);

public sealed record DashboardIssueStatisticsDto(
    IReadOnlyList<DashboardChartSliceDto> TypeBreakdown,
    IReadOnlyList<DashboardChartSliceDto> PriorityBreakdown);

public sealed record DashboardChartSliceDto(string Label, int Value);

public sealed record DashboardActivityDto(
    int Id,
    int? IssueId,
    string? IssueKey,
    string UserDisplayName,
    string? UserAvatarPath,
    ActivityActionType ActionType,
    string? FieldName,
    string? OldValue,
    string? NewValue,
    DateTime OccurredAtUtc);

public sealed record DashboardIssueDto(
    int Id,
    string IssueKey,
    string Title,
    IssueType Type,
    IssuePriority Priority,
    IssueStatus Status,
    int? StoryPoints,
    string ReporterName,
    IReadOnlyList<DashboardAssigneeDto> Assignees);

public sealed record DashboardAssigneeDto(int UserId, string DisplayName, string? AvatarPath);

public sealed record DashboardTeamWorkloadDto(int UserId, string DisplayName, string? AvatarPath, int OpenIssues, int InProgressIssues);
