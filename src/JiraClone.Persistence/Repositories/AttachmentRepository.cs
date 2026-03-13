using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class AttachmentRepository : IAttachmentRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public AttachmentRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Attachment>> GetByIssueIdAsync(int issueId, CancellationToken cancellationToken = default) =>
        await _dbContext.Attachments
            .Include(x => x.UploadedBy)
            .Where(x => x.IssueId == issueId && !x.IsDeleted)
            .OrderByDescending(x => x.UploadedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<Attachment?> GetByIdAsync(int attachmentId, CancellationToken cancellationToken = default) =>
        _dbContext.Attachments
            .Include(x => x.UploadedBy)
            .FirstOrDefaultAsync(x => x.Id == attachmentId && !x.IsDeleted, cancellationToken);

    public Task AddAsync(Attachment attachment, CancellationToken cancellationToken = default) =>
        _dbContext.Attachments.AddAsync(attachment, cancellationToken).AsTask();
}
