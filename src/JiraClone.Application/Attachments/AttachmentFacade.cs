using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Application.Attachments;

public class AttachmentFacade
{
    private readonly IAttachmentService _attachments;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IUnitOfWork _unitOfWork;

    public AttachmentFacade(
        IAttachmentService attachments,
        IAttachmentRepository attachmentRepository,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        IUnitOfWork unitOfWork)
    {
        _attachments = attachments;
        _attachmentRepository = attachmentRepository;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<Attachment> AddAsync(int issueId, int projectId, int userId, string sourceFilePath, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);
        var attachment = await _attachments.SaveAsync(issueId, userId, sourceFilePath, cancellationToken);
        await _attachmentRepository.AddAsync(attachment, cancellationToken);
        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = projectId,
            IssueId = issueId,
            UserId = userId,
            ActionType = ActivityActionType.AttachmentAdded,
            NewValue = attachment.OriginalFileName
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return attachment;
    }

    public Task<IReadOnlyList<Attachment>> GetByIssueAsync(int issueId, CancellationToken cancellationToken = default) =>
        _attachmentRepository.GetByIssueIdAsync(issueId, cancellationToken);

    public async Task<string?> ResolveDownloadPathAsync(int attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _attachmentRepository.GetByIdAsync(attachmentId, cancellationToken);
        return attachment is null ? null : await _attachments.ResolvePathAsync(attachment, cancellationToken);
    }

    public async Task<bool> SoftDeleteAsync(int attachmentId, int userId, int projectId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);
        var attachment = await _attachmentRepository.GetByIdAsync(attachmentId, cancellationToken);
        if (attachment is null)
        {
            return false;
        }

        await _attachments.DeleteAsync(attachment, cancellationToken);
        attachment.IsDeleted = true;
        attachment.UpdatedAtUtc = DateTime.UtcNow;

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = projectId,
            IssueId = attachment.IssueId,
            UserId = userId,
            ActionType = ActivityActionType.AttachmentRemoved,
            OldValue = attachment.OriginalFileName
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
