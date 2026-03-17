using JiraClone.Domain.Enums;

namespace JiraClone.Application.Models;

public sealed class IssueEditModel
{
    public int? Id { get; set; }
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? DescriptionText { get; set; }
    public IssueType Type { get; set; } = IssueType.Task;
    public IssueStatus Status { get; set; } = IssueStatus.Backlog;
    public IssuePriority Priority { get; set; } = IssuePriority.Medium;
    public int ReporterId { get; set; }
    public int CreatedById { get; set; }
    public int? EstimateHours { get; set; }
    public int? TimeSpentHours { get; set; }
    public int? TimeRemainingHours { get; set; }
    public int? StoryPoints { get; set; }
    public int? SprintId { get; set; }
    public int? ParentIssueId { get; set; }
    public IReadOnlyCollection<int> AssigneeIds { get; set; } = Array.Empty<int>();
}
