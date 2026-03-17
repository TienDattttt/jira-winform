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
    public int WorkflowStatusId { get; private set; }
    public WorkflowStatus WorkflowStatus { get; set; } = null!;
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
    public int? ParentIssueId { get; set; }
    public Issue? ParentIssue { get; set; }
    public int? FixVersionId { get; set; }
    public ProjectVersion? FixVersion { get; set; }
    public ICollection<Issue> SubIssues { get; set; } = new List<Issue>();
    public ICollection<IssueAssignee> Assignees { get; set; } = new List<IssueAssignee>();
    public ICollection<IssueLabel> IssueLabels { get; set; } = new List<IssueLabel>();
    public ICollection<IssueComponent> IssueComponents { get; set; } = new List<IssueComponent>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

    public void MoveTo(int workflowStatusId, decimal boardPosition)
    {
        WorkflowStatusId = workflowStatusId;
        BoardPosition = boardPosition;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
