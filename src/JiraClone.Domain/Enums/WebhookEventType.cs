namespace JiraClone.Domain.Enums;

public enum WebhookEventType
{
    IssueCreated = 1,
    IssueUpdated = 2,
    IssueDeleted = 3,
    IssueStatusChanged = 4,
    CommentAdded = 5,
    SprintStarted = 6,
    SprintCompleted = 7,
    ProjectUpdated = 8
}