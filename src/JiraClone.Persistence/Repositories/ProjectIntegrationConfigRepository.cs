using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class ProjectIntegrationConfigRepository : IProjectIntegrationConfigRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public ProjectIntegrationConfigRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ProjectIntegrationConfig?> GetByProjectAndNameAsync(int projectId, string integrationName, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<ProjectIntegrationConfig>()
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IntegrationName == integrationName, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectIntegrationConfig>> GetEnabledByIntegrationNameAsync(string integrationName, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<ProjectIntegrationConfig>()
            .Where(x => x.IntegrationName == integrationName && x.IsEnabled)
            .OrderBy(x => x.ProjectId)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(ProjectIntegrationConfig config, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<ProjectIntegrationConfig>().AddAsync(config, cancellationToken).AsTask();
    }

    public Task RemoveAsync(ProjectIntegrationConfig config, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<ProjectIntegrationConfig>().Remove(config);
        return Task.CompletedTask;
    }
}
