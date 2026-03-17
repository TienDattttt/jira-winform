using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class BoardColumn : AuditableEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int WorkflowStatusId { get; set; }
    public WorkflowStatus WorkflowStatus { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public int? WipLimit { get; set; }
}
