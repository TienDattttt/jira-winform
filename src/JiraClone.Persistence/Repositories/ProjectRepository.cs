using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public ProjectRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Project?> GetActiveProjectAsync(CancellationToken cancellationToken = default) =>
        _dbContext.Projects
            .Include(x => x.BoardColumns)
            .Include(x => x.Members)
            .ThenInclude(x => x.User)
            .ThenInclude(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.IsActive, cancellationToken);

    public Task<Project?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default) =>
        _dbContext.Projects
            .Include(x => x.BoardColumns)
            .Include(x => x.Members)
            .ThenInclude(x => x.User)
            .ThenInclude(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
}
