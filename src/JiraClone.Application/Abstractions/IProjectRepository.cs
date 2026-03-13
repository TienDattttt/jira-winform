using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IProjectRepository
{
    Task<Project?> GetActiveProjectAsync(CancellationToken cancellationToken = default);
    Task<Project?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default);
}
