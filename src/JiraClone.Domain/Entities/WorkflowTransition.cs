using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class WorkflowTransition : AuditableEntity
{
    public int WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public int FromStatusId { get; set; }
    public WorkflowStatus FromStatus { get; set; } = null!;
    public int ToStatusId { get; set; }
    public WorkflowStatus ToStatus { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public ICollection<Role> AllowedRoles { get; set; } = new List<Role>();
}
