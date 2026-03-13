using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface ICommentRepository
{
    Task<IReadOnlyList<Comment>> GetByIssueIdAsync(int issueId, CancellationToken cancellationToken = default);
    Task<Comment?> GetByIdAsync(int commentId, CancellationToken cancellationToken = default);
    Task AddAsync(Comment comment, CancellationToken cancellationToken = default);
}
