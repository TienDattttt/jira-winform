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

    public async Task<Project?> GetActiveProjectAsync(CancellationToken cancellationToken = default)
    {
        return await IncludeProjectGraph()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Project>> GetAccessibleProjectsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await IncludeProjectGraph()
            .Where(x => x.IsActive && x.Members.Any(member => member.UserId == userId))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Project?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default)
    {
        return await IncludeProjectGraph()
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
    }

    public async Task<Project?> GetDeleteSnapshotAsync(int projectId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects
            .AsSplitQuery()
            .Include(x => x.Members)
            .ThenInclude(x => x.User)
            .Include(x => x.BoardColumns)
            .ThenInclude(x => x.WorkflowStatus)
            .Include(x => x.Labels)
            .Include(x => x.Components)
            .Include(x => x.Versions)
            .Include(x => x.SavedFilters)
            .Include(x => x.WorkflowDefinitions)
            .ThenInclude(x => x.Statuses)
            .Include(x => x.WorkflowDefinitions)
            .ThenInclude(x => x.Transitions)
            .ThenInclude(x => x.AllowedRoles)
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
    }

    public Task<bool> ExistsByKeyAsync(string key, int? excludeProjectId = null, CancellationToken cancellationToken = default)
    {
        var normalizedKey = (key ?? string.Empty).Trim().ToUpperInvariant();
        return _dbContext.Projects.AnyAsync(
            x => x.Key == normalizedKey && (!excludeProjectId.HasValue || x.Id != excludeProjectId.Value),
            cancellationToken);
    }

    public Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        return _dbContext.Projects.AddAsync(project, cancellationToken).AsTask();
    }

    public Task DeleteAsync(Project project, CancellationToken cancellationToken = default)
    {
        _dbContext.Projects.Remove(project);
        return Task.CompletedTask;
    }

    private IQueryable<Project> IncludeProjectGraph()
    {
        return _dbContext.Projects
            .AsSplitQuery()
            .Include(x => x.BoardColumns)
            .ThenInclude(x => x.WorkflowStatus)
            .Include(x => x.Members)
            .ThenInclude(x => x.User)
            .ThenInclude(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Include(x => x.Labels)
            .Include(x => x.Components)
            .ThenInclude(x => x.LeadUser)
            .Include(x => x.Versions)
            .Include(x => x.WorkflowDefinitions)
            .ThenInclude(x => x.Statuses)
            .Include(x => x.WorkflowDefinitions)
            .ThenInclude(x => x.Transitions)
            .ThenInclude(x => x.FromStatus)
            .Include(x => x.WorkflowDefinitions)
            .ThenInclude(x => x.Transitions)
            .ThenInclude(x => x.ToStatus)
            .Include(x => x.WorkflowDefinitions)
            .ThenInclude(x => x.Transitions)
            .ThenInclude(x => x.AllowedRoles);
    }
}
