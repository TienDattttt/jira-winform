namespace JiraClone.Application.Models;

public sealed record BurndownReportDto(
    int SprintId,
    string SprintName,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalStoryPoints,
    IReadOnlyList<BurndownPointDto> IdealPoints,
    IReadOnlyList<BurndownPointDto> ActualPoints);

public sealed record BurndownPointDto(DateOnly Date, int DayNumber, double RemainingStoryPoints);

public sealed record VelocityReportDto(
    int ProjectId,
    IReadOnlyList<VelocitySprintDto> Sprints,
    double AverageCompletedStoryPoints);

public sealed record VelocitySprintDto(
    int SprintId,
    string SprintName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int CommittedStoryPoints,
    int CompletedStoryPoints);

public sealed record CfdDataPointDto(
    DateOnly Date,
    int StatusId,
    string Status,
    string Color,
    JiraClone.Domain.Enums.StatusCategory Category,
    int DisplayOrder,
    int IssueCount);

public sealed record SprintReportDto(
    int SprintId,
    string SprintName,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime? ClosedAtUtc,
    IReadOnlyList<SprintReportIssueDto> CompletedWork,
    IReadOnlyList<SprintReportIssueDto> NotCompleted,
    IReadOnlyList<SprintReportIssueDto> RemovedFromSprint,
    int CommittedStoryPoints,
    int CompletedStoryPoints,
    double CompletionPercentage);

public sealed record SprintReportIssueDto(
    int IssueId,
    string IssueKey,
    string Title,
    JiraClone.Domain.Enums.IssueType Type,
    string StatusName,
    JiraClone.Domain.Enums.StatusCategory StatusCategory,
    int StoryPoints,
    string AssigneeSummary);
