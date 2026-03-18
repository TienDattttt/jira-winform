using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IWatcherRepository
{
    Task<Watcher?> GetAsync(int issueId, int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Watcher>> GetByIssueIdAsync(int issueId, CancellationToken cancellationToken = default);
    Task AddAsync(Watcher watcher, CancellationToken cancellationToken = default);
    Task RemoveAsync(Watcher watcher, CancellationToken cancellationToken = default);
}
