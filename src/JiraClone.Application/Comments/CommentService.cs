using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Application.Comments;

public class CommentService
{
    private readonly ICommentRepository _comments;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IUnitOfWork _unitOfWork;

    public CommentService(ICommentRepository comments, IAuthorizationService authorization, IActivityLogRepository activityLogs, IUnitOfWork unitOfWork)
    {
        _comments = comments;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<Comment> AddAsync(int issueId, int userId, int projectId, string body, CancellationToken cancellationToken = default)
    {
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
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);
        var comment = await _comments.GetByIdAsync(commentId, cancellationToken);
        if (comment is null)
        {
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
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);
        var comment = await _comments.GetByIdAsync(commentId, cancellationToken);
        if (comment is null)
        {
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
