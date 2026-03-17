using JiraClone.Domain.Common;
using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class WorkflowStatus : AuditableEntity
{
    public int WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#42526E";
    public StatusCategory Category { get; set; } = StatusCategory.ToDo;
    public int DisplayOrder { get; set; }
    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
    public ICollection<BoardColumn> BoardColumns { get; set; } = new List<BoardColumn>();
    public ICollection<WorkflowTransition> OutgoingTransitions { get; set; } = new List<WorkflowTransition>();
    public ICollection<WorkflowTransition> IncomingTransitions { get; set; } = new List<WorkflowTransition>();
}
