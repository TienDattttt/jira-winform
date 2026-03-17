using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class ProjectVersionRepository : IProjectVersionRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public ProjectVersionRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProjectVersion>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default) =>
        await _dbContext.ProjectVersions
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.IsReleased)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<ProjectVersion?> GetByIdAsync(int versionId, CancellationToken cancellationToken = default) =>
        _dbContext.ProjectVersions.FirstOrDefaultAsync(x => x.Id == versionId, cancellationToken);

    public Task AddAsync(ProjectVersion version, CancellationToken cancellationToken = default) =>
        _dbContext.ProjectVersions.AddAsync(version, cancellationToken).AsTask();

    public Task RemoveAsync(ProjectVersion version, CancellationToken cancellationToken = default)
    {
        _dbContext.ProjectVersions.Remove(version);
        return Task.CompletedTask;
    }
}
