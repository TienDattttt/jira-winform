using JiraClone.Domain.Enums;

namespace JiraClone.Application.Models;

public sealed record BoardColumnDto(IssueStatus Status, string Name, IReadOnlyList<IssueSummaryDto> Issues);
