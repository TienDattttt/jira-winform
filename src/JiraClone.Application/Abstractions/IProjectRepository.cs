using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IProjectRepository
{
    Task<Project?> GetActiveProjectAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> GetAccessibleProjectsAsync(int userId, CancellationToken cancellationToken = default);
    Task<Project?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default);
    Task<Project?> GetDeleteSnapshotAsync(int projectId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByKeyAsync(string key, int? excludeProjectId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Project project, CancellationToken cancellationToken = default);
    Task DeleteAsync(Project project, CancellationToken cancellationToken = default);
}