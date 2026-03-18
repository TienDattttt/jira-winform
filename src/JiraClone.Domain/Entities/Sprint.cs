using JiraClone.Domain.Common;
using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class Sprint : AggregateRoot
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public SprintState State { get; set; } = SprintState.Planned;
    public DateTime? ClosedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
}