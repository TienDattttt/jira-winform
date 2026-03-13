using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class CommentRepository : ICommentRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public CommentRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Comment>> GetByIssueIdAsync(int issueId, CancellationToken cancellationToken = default) =>
        await _dbContext.Comments
            .Include(x => x.User)
            .Where(x => x.IssueId == issueId && !x.IsDeleted)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<Comment?> GetByIdAsync(int commentId, CancellationToken cancellationToken = default) =>
        _dbContext.Comments
            .Include(x => x.User)
            .Include(x => x.Issue)
            .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);

    public Task AddAsync(Comment comment, CancellationToken cancellationToken = default) =>
        _dbContext.Comments.AddAsync(comment, cancellationToken).AsTask();
}
