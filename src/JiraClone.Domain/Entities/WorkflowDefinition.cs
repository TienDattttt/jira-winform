using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class WorkflowDefinition : AggregateRoot
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public ICollection<WorkflowStatus> Statuses { get; set; } = new List<WorkflowStatus>();
    public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
}
