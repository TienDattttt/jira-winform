namespace JiraClone.Domain.Enums;

public enum Permission
{
    CreateIssue = 1,
    EditIssue = 2,
    DeleteIssue = 3,
    TransitionIssue = 4,
    ManageSprints = 5,
    ManageBoard = 6,
    ManageProject = 7,
    ManageMembers = 8,
    ViewProject = 9,
    AddComment = 10,
    EditOwnComment = 11,
    DeleteOwnComment = 12
}
