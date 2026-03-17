using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class Component : AggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int? LeadUserId { get; set; }
    public User? LeadUser { get; set; }
    public ICollection<IssueComponent> IssueComponents { get; set; } = new List<IssueComponent>();
}
