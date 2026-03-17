using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IVersionService
{
    Task<IReadOnlyList<ProjectVersion>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<ProjectVersion> CreateAsync(int projectId, string name, string? description, DateTime? releaseDate, CancellationToken cancellationToken = default);
    Task<ProjectVersion?> UpdateAsync(int versionId, string name, string? description, DateTime? releaseDate, bool isReleased, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int versionId, CancellationToken cancellationToken = default);
    Task<bool> AssignToIssueAsync(int issueId, int? versionId, CancellationToken cancellationToken = default);
    Task<ProjectVersion?> MarkReleasedAsync(int versionId, DateTime? releaseDate = null, CancellationToken cancellationToken = default);
}
