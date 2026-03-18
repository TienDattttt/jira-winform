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
    int? ParentIssueId = null,
    string? ParentIssueKey = null,
    int? EpicId = null,
    string? EpicKey = null,
    string? EpicTitle = null,
    string? EpicColor = null,
    int? StoryPoints = null,
    DateOnly? DueDate = null);
