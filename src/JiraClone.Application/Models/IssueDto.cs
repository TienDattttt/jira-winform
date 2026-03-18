using JiraClone.Domain.Enums;

namespace JiraClone.Application.Models;

public sealed record IssueDto(
    int Id,
    string IssueKey,
    string Title,
    IssueType Type,
    IssuePriority Priority,
    int WorkflowStatusId,
    string WorkflowStatusName,
    string WorkflowStatusColor,
    StatusCategory WorkflowStatusCategory,
    string ProjectKey,
    string ProjectName,
    int ReporterId,
    string ReporterName,
    IReadOnlyList<string> AssigneeNames,
    int? SprintId,
    string? SprintName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateOnly? DueDate,
    int? StoryPoints,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> Components);
