using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class ProjectMember
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public ProjectRole ProjectRole { get; set; } = ProjectRole.Developer;
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
