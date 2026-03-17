using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class ComponentRepository : IComponentRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public ComponentRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Component>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default) =>
        await _dbContext.Components
            .Include(x => x.LeadUser)
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<Component?> GetByIdAsync(int componentId, CancellationToken cancellationToken = default) =>
        _dbContext.Components
            .Include(x => x.LeadUser)
            .FirstOrDefaultAsync(x => x.Id == componentId, cancellationToken);

    public Task AddAsync(Component component, CancellationToken cancellationToken = default) =>
        _dbContext.Components.AddAsync(component, cancellationToken).AsTask();

    public Task RemoveAsync(Component component, CancellationToken cancellationToken = default)
    {
        _dbContext.Components.Remove(component);
        return Task.CompletedTask;
    }
}
