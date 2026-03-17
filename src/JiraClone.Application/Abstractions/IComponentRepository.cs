using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IComponentRepository
{
    Task<IReadOnlyList<Component>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<Component?> GetByIdAsync(int componentId, CancellationToken cancellationToken = default);
    Task AddAsync(Component component, CancellationToken cancellationToken = default);
    Task RemoveAsync(Component component, CancellationToken cancellationToken = default);
}
