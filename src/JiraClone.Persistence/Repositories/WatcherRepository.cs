using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class WatcherRepository : IWatcherRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public WatcherRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Watcher?> GetAsync(int issueId, int userId, CancellationToken cancellationToken = default) =>
        _dbContext.Watchers
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.IssueId == issueId && x.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Watcher>> GetByIssueIdAsync(int issueId, CancellationToken cancellationToken = default) =>
        await _dbContext.Watchers
            .Include(x => x.User)
            .Where(x => x.IssueId == issueId)
            .OrderBy(x => x.User.DisplayName)
            .ToListAsync(cancellationToken);

    public Task AddAsync(Watcher watcher, CancellationToken cancellationToken = default) =>
        _dbContext.Watchers.AddAsync(watcher, cancellationToken).AsTask();

    public Task RemoveAsync(Watcher watcher, CancellationToken cancellationToken = default)
    {
        _dbContext.Watchers.Remove(watcher);
        return Task.CompletedTask;
    }
}
