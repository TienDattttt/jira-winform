using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface ILabelService
{
    Task<IReadOnlyList<Label>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<Label> CreateAsync(int projectId, string name, string color, CancellationToken cancellationToken = default);
    Task<Label?> UpdateAsync(int labelId, string name, string color, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int labelId, CancellationToken cancellationToken = default);
    Task<bool> AssignToIssueAsync(int issueId, IReadOnlyCollection<int> labelIds, CancellationToken cancellationToken = default);
}
