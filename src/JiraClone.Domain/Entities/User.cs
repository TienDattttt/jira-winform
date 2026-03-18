using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class User : AggregateRoot
{
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string? LastRefreshToken { get; set; }
    public DateTime? SessionExpiresAtUtc { get; set; }
    public string? AvatarPath { get; set; }
    public bool IsActive { get; set; } = true;
    public bool EmailNotificationsEnabled { get; set; } = true;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<ProjectMember> ProjectMemberships { get; set; } = new List<ProjectMember>();
    public ICollection<Component> LedComponents { get; set; } = new List<Component>();
    public ICollection<SavedFilter> SavedFilters { get; set; } = new List<SavedFilter>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<IssueAssignee> AssignedIssues { get; set; } = new List<IssueAssignee>();
    public ICollection<Watcher> WatchedIssues { get; set; } = new List<Watcher>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<ApiToken> ApiTokens { get; set; } = new List<ApiToken>();
}
