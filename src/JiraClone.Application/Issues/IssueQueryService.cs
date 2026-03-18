using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Issues;

public class IssueQueryService : IIssueQueryService
{
    private readonly IIssueRepository _issues;

    public IssueQueryService(IIssueRepository issues)
    {
        _issues = issues;
    }

    public async Task<IReadOnlyList<DashboardIssueDto>> GetProjectIssuesAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var issues = await _issues.GetBoardIssuesAsync(projectId, cancellationToken);
        return issues
            .OrderBy(issue => issue.WorkflowStatus.DisplayOrder)
            .ThenBy(issue => issue.BoardPosition)
            .Select(issue => new DashboardIssueDto(
                issue.Id,
                issue.IssueKey,
                issue.Title,
                issue.Type,
                issue.Priority,
                issue.WorkflowStatusId,
                issue.WorkflowStatus.Name,
                issue.WorkflowStatus.Color,
                issue.WorkflowStatus.Category,
                issue.StoryPoints,
                issue.Reporter.DisplayName,
                issue.Assignees
                    .OrderBy(assignee => assignee.User.DisplayName)
                    .Select(assignee => new DashboardAssigneeDto(assignee.UserId, assignee.User.DisplayName, assignee.User.AvatarPath))
                    .ToList()))
            .ToList();
    }
}
