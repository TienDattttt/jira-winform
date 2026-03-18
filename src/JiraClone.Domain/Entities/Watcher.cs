namespace JiraClone.Domain.Entities;

public class Watcher
{
    public int IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime WatchedAtUtc { get; set; } = DateTime.UtcNow;
}
