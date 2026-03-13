using JiraClone.Domain.Common;
using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class Issue : AggregateRoot
{
    private int? _storyPoints;

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int? SprintId { get; set; }
    public Sprint? Sprint { get; set; }
    public string IssueKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? DescriptionHtml { get; set; }
    public string? DescriptionText { get; set; }
    public IssueType Type { get; set; } = IssueType.Task;
    public IssueStatus Status { get; private set; } = IssueStatus.Backlog;
    public IssuePriority Priority { get; set; } = IssuePriority.Medium;
    public int ReporterId { get; set; }
    public User Reporter { get; set; } = null!;
    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public int? EstimateHours { get; set; }
    public int? TimeSpentHours { get; set; }
    public int? TimeRemainingHours { get; set; }
    public int? StoryPoints
    {
        get => _storyPoints;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(StoryPoints), "Story points cannot be negative.");
            }

            _storyPoints = value;
        }
    }
    public DateOnly? DueDate { get; set; }
    public decimal BoardPosition { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<IssueAssignee> Assignees { get; set; } = new List<IssueAssignee>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

    public void MoveTo(IssueStatus status, decimal boardPosition)
    {
        Status = status;
        BoardPosition = boardPosition;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
