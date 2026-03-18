using JiraClone.Domain.Enums;

namespace JiraClone.Application.Models;

public sealed record RoadmapEpicDto(
    int EpicId,
    string IssueKey,
    string Title,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string Status,
    StatusCategory StatusCategory,
    string Color,
    int? AssigneeId,
    string? AssigneeName,
    int ChildIssueCount,
    int DoneCount,
    int TotalStoryPoints,
    int DoneStoryPoints,
    IReadOnlyList<int> SprintIds);