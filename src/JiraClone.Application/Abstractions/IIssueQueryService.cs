using JiraClone.Application.Models;

namespace JiraClone.Application.Abstractions;

public interface IIssueQueryService
{
    Task<IReadOnlyList<DashboardIssueDto>> GetProjectIssuesAsync(int projectId, CancellationToken cancellationToken = default);
}
