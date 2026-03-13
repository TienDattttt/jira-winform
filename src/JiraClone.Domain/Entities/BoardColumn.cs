using JiraClone.Domain.Common;
using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class BoardColumn : AuditableEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public IssueStatus StatusCode { get; set; }
    public int DisplayOrder { get; set; }
    public int? WipLimit { get; set; }
}
