using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Boards;

public class BoardQueryService : IBoardQueryService
{
    private readonly IIssueRepository _issues;
    private readonly IProjectRepository _projects;
    private readonly ILogger<BoardQueryService> _logger;

    public BoardQueryService(IIssueRepository issues, IProjectRepository projects, ILogger<BoardQueryService>? logger = null)
    {
        _issues = issues;
        _projects = projects;
        _logger = logger ?? NullLogger<BoardQueryService>.Instance;
    }

    public async Task<IReadOnlyList<BoardColumnDto>> GetBoardAsync(int projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading board for project {ProjectId}.", projectId);
        return await GetBoardAsync(projectId, sprintId: null, cancellationToken);
    }

    public async Task<IReadOnlyList<BoardColumnDto>> GetBoardAsync(int projectId, int? sprintId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading board for project {ProjectId} and sprint {SprintId}.", projectId, sprintId);
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            _logger.LogWarning("Board request skipped because project {ProjectId} was not found.", projectId);
            return [];
        }

        var issues = await _issues.GetBoardIssuesAsync(projectId, sprintId, cancellationToken);
        var issuesByStatusId = issues.GroupBy(issue => issue.WorkflowStatusId).ToDictionary(group => group.Key, group => group.OrderBy(x => x.BoardPosition).ToList());

        return project.BoardColumns
            .OrderBy(column => column.DisplayOrder)
            .Select(column =>
            {
                var items = issuesByStatusId.GetValueOrDefault(column.WorkflowStatusId, [])
                    .Select(issue => new IssueSummaryDto(
                        issue.Id,
                        issue.IssueKey,
                        issue.Title,
                        issue.Type,
                        issue.Priority,
                        issue.WorkflowStatusId,
                        issue.WorkflowStatus.Name,
                        issue.WorkflowStatus.Color,
                        issue.WorkflowStatus.Category,
                        issue.BoardPosition,
                        issue.Reporter.DisplayName,
                        issue.Assignees.Select(a => a.User.DisplayName).ToList(),
                        issue.ParentIssue?.IssueKey,
                        issue.StoryPoints))
                    .ToList();

                return new BoardColumnDto(
                    column.Id,
                    column.WorkflowStatusId,
                    column.Name,
                    column.WorkflowStatus.Color,
                    column.WorkflowStatus.Category,
                    column.DisplayOrder,
                    column.WipLimit,
                    items);
            })
            .ToList();
    }
}
