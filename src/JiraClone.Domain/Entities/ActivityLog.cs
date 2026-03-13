using JiraClone.Domain.Common;
using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class ActivityLog : AuditableEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int? IssueId { get; set; }
    public Issue? Issue { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public ActivityActionType ActionType { get; set; }
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? MetadataJson { get; set; }
}
