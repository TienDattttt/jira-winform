using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IAttachmentRepository
{
    Task<IReadOnlyList<Attachment>> GetByIssueIdAsync(int issueId, CancellationToken cancellationToken = default);
    Task<Attachment?> GetByIdAsync(int attachmentId, CancellationToken cancellationToken = default);
    Task AddAsync(Attachment attachment, CancellationToken cancellationToken = default);
}
