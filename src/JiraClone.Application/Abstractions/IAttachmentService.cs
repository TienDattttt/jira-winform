using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IAttachmentService
{
    Task<Attachment> SaveAsync(int issueId, int uploadedById, string sourceFilePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(Attachment attachment, CancellationToken cancellationToken = default);
    Task<string> ResolvePathAsync(Attachment attachment, CancellationToken cancellationToken = default);
}
