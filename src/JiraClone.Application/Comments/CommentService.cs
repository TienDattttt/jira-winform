using System.Text.RegularExpressions;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Common;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Comments;

public class CommentService
{
    private static readonly Regex MentionRegex = new("@([A-Za-z0-9._-]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ICommentRepository _comments;
    private readonly IIssueRepository _issues;
    private readonly IUserRepository _users;
    private readonly IWatcherRepository _watchers;
    private readonly INotificationService _notificationService;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CommentService> _logger;

    public CommentService(
        ICommentRepository comments,
        IIssueRepository issues,
        IUserRepository users,
        IWatcherRepository watchers,
        INotificationService notificationService,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        IUnitOfWork unitOfWork,
        ILogger<CommentService>? logger = null)
    {
        _comments = comments;
        _issues = issues;
        _users = users;
        _watchers = watchers;
        _notificationService = notificationService;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<CommentService>.Instance;
    }

    public async Task<Comment> AddAsync(int issueId, int userId, int projectId, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding comment to issue {IssueId} by user {UserId}.", issueId, userId);
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken)
            ?? throw new NotFoundException($"Issue with id {issueId} was not found.");
        var actor = await _users.GetByIdAsync(userId, cancellationToken);
        var actorName = actor?.DisplayName ?? "Someone";
        var normalizedBody = body.Trim();
        var comment = new Comment
        {
            IssueId = issueId,
            UserId = userId,
            Body = normalizedBody
        };

        await _comments.AddAsync(comment, cancellationToken);
        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            IssueId = issueId,
            ProjectId = projectId,
            UserId = userId,
            ActionType = ActivityActionType.CommentAdded,
            NewValue = normalizedBody
        }, cancellationToken);

        var mentionedUserIds = await ResolveMentionedUserIdsAsync(projectId, normalizedBody, userId, cancellationToken);
        foreach (var recipientUserId in mentionedUserIds)
        {
            await _notificationService.CreateNotificationAsync(
                recipientUserId,
                NotificationType.CommentMentioned,
                $"Mentioned on {issue.IssueKey}",
                $"{actorName} mentioned you in a comment on {issue.IssueKey} - {issue.Title}.",
                issue.Id,
                issue.ProjectId,
                cancellationToken);
        }

        var watcherRecipientIds = (await _watchers.GetByIssueIdAsync(issueId, cancellationToken))
            .Select(x => x.UserId)
            .Where(recipientUserId => recipientUserId != userId && !mentionedUserIds.Contains(recipientUserId))
            .Distinct()
            .ToList();
        foreach (var recipientUserId in watcherRecipientIds)
        {
            await _notificationService.CreateNotificationAsync(
                recipientUserId,
                NotificationType.CommentAdded,
                $"New comment: {issue.IssueKey}",
                $"{actorName} commented on {issue.IssueKey}: {BuildExcerpt(normalizedBody)}",
                issue.Id,
                issue.ProjectId,
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return comment;
    }

    public async Task<Comment?> UpdateAsync(int commentId, int userId, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating comment {CommentId} by user {UserId}.", commentId, userId);
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);
        var comment = await _comments.GetByIdAsync(commentId, cancellationToken);
        if (comment is null)
        {
            _logger.LogWarning("Comment {CommentId} was not found for update.", commentId);
            return null;
        }

        comment.Body = body.Trim();
        comment.UpdatedAtUtc = DateTime.UtcNow;

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            IssueId = comment.IssueId,
            ProjectId = comment.Issue.ProjectId,
            UserId = userId,
            ActionType = ActivityActionType.CommentUpdated,
            NewValue = comment.Body
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return comment;
    }

    public async Task<bool> SoftDeleteAsync(int commentId, int userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Soft-deleting comment {CommentId} by user {UserId}.", commentId, userId);
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);
        var comment = await _comments.GetByIdAsync(commentId, cancellationToken);
        if (comment is null)
        {
            _logger.LogWarning("Comment {CommentId} was not found for soft delete.", commentId);
            return false;
        }

        comment.IsDeleted = true;
        comment.UpdatedAtUtc = DateTime.UtcNow;

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            IssueId = comment.IssueId,
            ProjectId = comment.Issue.ProjectId,
            UserId = userId,
            ActionType = ActivityActionType.CommentDeleted,
            OldValue = comment.Body
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<HashSet<int>> ResolveMentionedUserIdsAsync(int projectId, string body, int actorUserId, CancellationToken cancellationToken)
    {
        var mentionedUserNames = MentionRegex.Matches(body)
            .Select(match => match.Groups[1].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (mentionedUserNames.Count == 0)
        {
            return [];
        }

        return (await _users.GetProjectUsersAsync(projectId, cancellationToken))
            .Where(user => user.Id != actorUserId && mentionedUserNames.Contains(user.UserName))
            .Select(user => user.Id)
            .ToHashSet();
    }

    private static string BuildExcerpt(string value)
    {
        var trimmed = value.Replace(Environment.NewLine, " ").Trim();
        return trimmed.Length <= 140 ? trimmed : $"{trimmed[..137]}...";
    }
}

