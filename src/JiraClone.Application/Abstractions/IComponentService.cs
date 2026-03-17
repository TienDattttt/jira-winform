using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IComponentService
{
    Task<IReadOnlyList<Component>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<Component> CreateAsync(int projectId, string name, string? description, int? leadUserId, CancellationToken cancellationToken = default);
    Task<Component?> UpdateAsync(int componentId, string name, string? description, int? leadUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int componentId, CancellationToken cancellationToken = default);
    Task<bool> AssignToIssueAsync(int issueId, int? componentId, CancellationToken cancellationToken = default);
}
