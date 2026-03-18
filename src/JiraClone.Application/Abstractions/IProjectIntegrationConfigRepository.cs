using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IProjectIntegrationConfigRepository
{
    Task<ProjectIntegrationConfig?> GetByProjectAndNameAsync(int projectId, string integrationName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectIntegrationConfig>> GetEnabledByIntegrationNameAsync(string integrationName, CancellationToken cancellationToken = default);
    Task AddAsync(ProjectIntegrationConfig config, CancellationToken cancellationToken = default);
    Task RemoveAsync(ProjectIntegrationConfig config, CancellationToken cancellationToken = default);
}
