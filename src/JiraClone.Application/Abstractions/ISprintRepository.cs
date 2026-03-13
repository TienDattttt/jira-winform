using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface ISprintRepository
{
    Task<IReadOnlyList<Sprint>> GetByProjectIdAsync(int projectId, CancellationToken cancellationToken = default);
    Task<Sprint?> GetByIdAsync(int sprintId, CancellationToken cancellationToken = default);
    Task<Sprint?> GetActiveByProjectIdAsync(int projectId, CancellationToken cancellationToken = default);
    Task AddAsync(Sprint sprint, CancellationToken cancellationToken = default);
}
