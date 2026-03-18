namespace JiraClone.Domain.Enums;

public enum NotificationType
{
    IssueAssigned = 1,
    IssueStatusChanged = 2,
    CommentAdded = 3,
    CommentMentioned = 4,
    SprintStarted = 5,
    SprintCompleted = 6,
    IssueWatcherUpdate = 7
}
