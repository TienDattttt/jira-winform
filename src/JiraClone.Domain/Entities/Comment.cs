using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class Comment : AggregateRoot
{
    public int IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
