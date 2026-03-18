using JiraClone.Domain.Enums;

namespace JiraClone.Application.Models;

public sealed record IssueSummaryDto(
    int Id,
    string IssueKey,
    string Title,
    IssueType Type,
    IssuePriority Priority,
    int StatusId,
    string StatusName,
    string StatusColor,
    StatusCategory StatusCategory,
    decimal BoardPosition,
    string ReporterName,
    IReadOnlyList<string> AssigneeNames,
    string? ParentIssueKey = null,
    int? StoryPoints = null);
