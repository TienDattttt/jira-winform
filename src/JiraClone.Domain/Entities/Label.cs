using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class Label : AggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#4688EC";
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public ICollection<IssueLabel> IssueLabels { get; set; } = new List<IssueLabel>();
}
