using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface ISavedFilterRepository
{
    Task<IReadOnlyList<SavedFilter>> GetByProjectAsync(int projectId, int userId, CancellationToken cancellationToken = default);
    Task<SavedFilter?> GetByIdAsync(int savedFilterId, CancellationToken cancellationToken = default);
    Task AddAsync(SavedFilter savedFilter, CancellationToken cancellationToken = default);
    Task RemoveAsync(SavedFilter savedFilter, CancellationToken cancellationToken = default);
}
