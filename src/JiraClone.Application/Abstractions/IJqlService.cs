using JiraClone.Application.Jql;
using JiraClone.Application.Models;

namespace JiraClone.Application.Abstractions;

public interface IJqlService
{
    Task<IReadOnlyList<IssueDto>> ExecuteQueryAsync(string? jql, int projectId, CancellationToken cancellationToken = default);
    JqlQuery Parse(string? jql);
}
