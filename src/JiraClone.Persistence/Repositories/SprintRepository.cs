using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class SprintRepository : ISprintRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public SprintRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Sprint>> GetByProjectIdAsync(int projectId, CancellationToken cancellationToken = default) =>
        await _dbContext.Sprints
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);

    public Task<Sprint?> GetByIdAsync(int sprintId, CancellationToken cancellationToken = default) =>
        _dbContext.Sprints.FirstOrDefaultAsync(x => x.Id == sprintId, cancellationToken);

    public Task<Sprint?> GetActiveByProjectIdAsync(int projectId, CancellationToken cancellationToken = default) =>
        _dbContext.Sprints
            .Where(x => x.ProjectId == projectId && x.State == Domain.Enums.SprintState.Active)
            .OrderByDescending(x => x.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(Sprint sprint, CancellationToken cancellationToken = default) =>
        _dbContext.Sprints.AddAsync(sprint, cancellationToken).AsTask();
}
