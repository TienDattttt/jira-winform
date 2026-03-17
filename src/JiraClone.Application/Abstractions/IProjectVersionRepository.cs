using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IProjectVersionRepository
{
    Task<IReadOnlyList<ProjectVersion>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<ProjectVersion?> GetByIdAsync(int versionId, CancellationToken cancellationToken = default);
    Task AddAsync(ProjectVersion version, CancellationToken cancellationToken = default);
    Task RemoveAsync(ProjectVersion version, CancellationToken cancellationToken = default);
}
