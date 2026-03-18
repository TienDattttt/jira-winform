using JiraClone.Domain.Common;
using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class Notification : AggregateRoot
{
    public int RecipientUserId { get; set; }
    public User RecipientUser { get; set; } = null!;
    public int? IssueId { get; set; }
    public Issue? Issue { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
