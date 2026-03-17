using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface ILabelRepository
{
    Task<IReadOnlyList<Label>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<Label?> GetByIdAsync(int labelId, CancellationToken cancellationToken = default);
    Task AddAsync(Label label, CancellationToken cancellationToken = default);
    Task RemoveAsync(Label label, CancellationToken cancellationToken = default);
}
