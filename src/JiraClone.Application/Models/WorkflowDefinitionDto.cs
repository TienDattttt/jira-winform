using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Models;

public sealed record WorkflowStatusOptionDto(int Id, string Name, string Color, StatusCategory Category, int DisplayOrder)
{
    public override string ToString() => Name;
}

public sealed record WorkflowTransitionDto(int Id, int FromStatusId, string FromStatusName, int ToStatusId, string ToStatusName, string Name, IReadOnlyList<string> AllowedRoleNames);

public sealed record WorkflowDefinitionDto(int Id, int ProjectId, string Name, bool IsDefault, IReadOnlyList<WorkflowStatusOptionDto> Statuses, IReadOnlyList<WorkflowTransitionDto> Transitions);

public sealed record WorkflowTransitionResult(bool Succeeded, WorkflowStatus? PreviousStatus, WorkflowStatus? CurrentStatus, decimal BoardPosition);
