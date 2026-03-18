using JiraClone.Application.Models;

namespace JiraClone.Application.Abstractions;

public interface ISavedFilterService
{
    Task<IReadOnlyList<SavedFilterDto>> GetByProjectAsync(int projectId, int userId, CancellationToken cancellationToken = default);
    Task<SavedFilterDto> CreateAsync(int projectId, int userId, string name, string queryText, bool isFavorite = false, CancellationToken cancellationToken = default);
    Task<SavedFilterDto?> UpdateAsync(int savedFilterId, int userId, string name, string queryText, bool isFavorite = false, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int savedFilterId, int userId, CancellationToken cancellationToken = default);
}
