using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Comments;

public class CommentService
{
    private readonly ICommentRepository _comments;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CommentService> _logger;

    public CommentService(ICommentRepository comments, IAuthorizationService authorization, IActivityLogRepository activityLogs, IUnitOfWork unitOfWork, ILogger<CommentService>? logger = null)
    {
        _comments = comments;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<CommentService>.Instance;
    }

    public async Task<Comment> AddAsync(int issueId, int userId, int projectId, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding comment to issue {IssueId} by user {UserId}.", issueId, userId);
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);
        var comment = new Comment
        {
            IssueId = issueId,
            UserId = userId,
            Body = body.Trim()
        };

        await _comments.AddAsync(comment, cancellationToken);
        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            IssueId = issueId,
            ProjectId = projectId,
            UserId = userId,
            ActionType = ActivityActionType.CommentAdded,
            NewValue = body
        }, cancellationToken);

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
}
