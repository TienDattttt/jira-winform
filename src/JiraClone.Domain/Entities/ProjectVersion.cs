using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class ProjectVersion : AggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public DateTime? ReleaseDate { get; set; }
    public bool IsReleased { get; set; }
    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
}
