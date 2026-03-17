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
