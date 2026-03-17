using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Boards;

public class BoardQueryService
{
    private static readonly IReadOnlyDictionary<IssueStatus, string> DefaultColumnNames = new Dictionary<IssueStatus, string>
    {
        [IssueStatus.Backlog] = "Backlog",
        [IssueStatus.Selected] = "Selected",
        [IssueStatus.InProgress] = "In Progress",
        [IssueStatus.Done] = "Done"
    };

    private readonly IIssueRepository _issues;

    public BoardQueryService(IIssueRepository issues)
    {
        _issues = issues;
    }

    public async Task<IReadOnlyList<BoardColumnDto>> GetBoardAsync(int projectId, CancellationToken cancellationToken = default)
        => await GetBoardAsync(projectId, sprintId: null, cancellationToken);

    public async Task<IReadOnlyList<BoardColumnDto>> GetBoardAsync(int projectId, int? sprintId, CancellationToken cancellationToken = default)
    {
        var issues = await _issues.GetBoardIssuesAsync(projectId, sprintId, cancellationToken);

        return DefaultColumnNames.Keys
            .Select(status =>
            {
                var items = issues
                    .Where(issue => issue.Status == status)
                    .OrderBy(issue => issue.BoardPosition)
                    .Select(issue => new IssueSummaryDto(
                        issue.Id,
                        issue.IssueKey,
                        issue.Title,
                        issue.Type,
                        issue.Priority,
                        issue.Status,
                        issue.BoardPosition,
                        issue.Reporter.DisplayName,
                        issue.Assignees.Select(a => a.User.DisplayName).ToList(),
                        issue.ParentIssue?.IssueKey))
                    .ToList();

                return new BoardColumnDto(status, DefaultColumnNames[status], items);
            })
            .ToList();
    }
}
