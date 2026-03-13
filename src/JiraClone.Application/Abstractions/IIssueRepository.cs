using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Abstractions;

public interface IIssueRepository
{
    Task<IReadOnlyList<Issue>> GetBoardIssuesAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Issue>> GetBoardIssuesAsync(int projectId, int? sprintId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Issue>> GetProjectIssuesAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Issue>> GetIncompleteBySprintIdAsync(int sprintId, CancellationToken cancellationToken = default);
    Task<Issue?> GetByIdAsync(int issueId, CancellationToken cancellationToken = default);
    Task<decimal> GetNextBoardPositionAsync(int projectId, IssueStatus status, CancellationToken cancellationToken = default);
    Task AddAsync(Issue issue, CancellationToken cancellationToken = default);
    Task RemoveAsync(Issue issue, CancellationToken cancellationToken = default);
}
