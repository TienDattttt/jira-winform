using JiraClone.Domain.Enums;

namespace JiraClone.Application.Models;

public sealed record BoardColumnDto(
    int BoardColumnId,
    int StatusId,
    string Name,
    string Color,
    StatusCategory Category,
    int DisplayOrder,
    int? WipLimit,
    IReadOnlyList<IssueSummaryDto> Issues,
    int TotalIssueCount);