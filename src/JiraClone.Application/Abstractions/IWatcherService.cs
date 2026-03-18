using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IWatcherService
{
    Task<bool> WatchIssueAsync(int issueId, int userId, CancellationToken cancellationToken = default);
    Task<bool> UnwatchIssueAsync(int issueId, int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetWatchersAsync(int issueId, CancellationToken cancellationToken = default);
    Task<bool> IsWatchingAsync(int issueId, int userId, CancellationToken cancellationToken = default);
}
