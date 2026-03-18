using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class SavedFilterRepository : ISavedFilterRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public SavedFilterRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SavedFilter>> GetByProjectAsync(int projectId, int userId, CancellationToken cancellationToken = default) =>
        await _dbContext.SavedFilters
            .Where(x => x.ProjectId == projectId && x.UserId == userId)
            .OrderByDescending(x => x.IsFavorite)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<SavedFilter?> GetByIdAsync(int savedFilterId, CancellationToken cancellationToken = default) =>
        _dbContext.SavedFilters.FirstOrDefaultAsync(x => x.Id == savedFilterId, cancellationToken);

    public Task AddAsync(SavedFilter savedFilter, CancellationToken cancellationToken = default) =>
        _dbContext.SavedFilters.AddAsync(savedFilter, cancellationToken).AsTask();

    public Task RemoveAsync(SavedFilter savedFilter, CancellationToken cancellationToken = default)
    {
        _dbContext.SavedFilters.Remove(savedFilter);
        return Task.CompletedTask;
    }
}
