using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class SavedFilter : AggregateRoot
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
}
