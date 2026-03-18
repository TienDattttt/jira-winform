using System.Text.Json;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Boards;

public class BoardQueryService : IBoardQueryService
{
    private static readonly string[] EpicColors = ["#357DE8", "#1F845A", "#FCA700", "#42B2D7", "#DE350B"];

    private readonly IIssueRepository _issues;
    private readonly IProjectRepository _projects;
    private readonly IActivityLogRepository _activityLogs;
    private readonly ILogger<BoardQueryService> _logger;

    public BoardQueryService(
        IIssueRepository issues,
        IProjectRepository projects,
        IActivityLogRepository activityLogs,
        ILogger<BoardQueryService>? logger = null)
    {
        _issues = issues;
        _projects = projects;
        _activityLogs = activityLogs;
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
        var issuesByStatusId = issues
            .GroupBy(issue => issue.WorkflowStatusId)
            .ToDictionary(group => group.Key, group => group.OrderBy(x => x.BoardPosition).ToList());

        return project.BoardColumns
            .OrderBy(column => column.DisplayOrder)
            .Select(column =>
            {
                var items = issuesByStatusId.GetValueOrDefault(column.WorkflowStatusId, [])
                    .Select(MapIssueSummary)
                    .ToList();

                return new BoardColumnDto(
                    column.Id,
                    column.WorkflowStatusId,
                    column.Name,
                    column.WorkflowStatus.Color,
                    column.WorkflowStatus.Category,
                    column.DisplayOrder,
                    column.WipLimit,
                    items,
                    items.Count);
            })
            .ToList();
    }

    public async Task<TimeSpan?> GetAverageCycleTimeAsync(int projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calculating average cycle time for project {ProjectId}.", projectId);
        var statusChanges = await _activityLogs.GetProjectStatusChangesAsync(projectId, cancellationToken);
        if (statusChanges.Count == 0)
        {
            return null;
        }

        var cycleTimes = new List<TimeSpan>();
        foreach (var issueGroup in statusChanges
                     .Where(log => log.IssueId.HasValue)
                     .GroupBy(log => log.IssueId!.Value))
        {
            DateTime? enteredInProgressAtUtc = null;
            foreach (var change in issueGroup.OrderBy(log => log.OccurredAtUtc).ThenBy(log => log.Id))
            {
                var metadata = TryParseTransitionMetadata(change.MetadataJson);
                if (metadata is null)
                {
                    continue;
                }

                switch (metadata.NewCategory)
                {
                    case StatusCategory.InProgress:
                        enteredInProgressAtUtc = change.OccurredAtUtc;
                        break;
                    case StatusCategory.Done when enteredInProgressAtUtc.HasValue && change.OccurredAtUtc >= enteredInProgressAtUtc.Value:
                        cycleTimes.Add(change.OccurredAtUtc - enteredInProgressAtUtc.Value);
                        enteredInProgressAtUtc = null;
                        break;
                    case StatusCategory.ToDo:
                        enteredInProgressAtUtc = null;
                        break;
                }
            }
        }

        return cycleTimes.Count == 0
            ? null
            : TimeSpan.FromTicks(Convert.ToInt64(cycleTimes.Average(duration => duration.Ticks)));
    }

    private static IssueSummaryDto MapIssueSummary(Issue issue)
    {
        var epic = ResolveEpic(issue);
        return new IssueSummaryDto(
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
            issue.ParentIssueId,
            issue.ParentIssue?.IssueKey,
            epic?.Id,
            epic?.IssueKey,
            epic?.Title,
            epic?.Color,
            issue.StoryPoints,
            issue.DueDate);
    }

    private static EpicSummary? ResolveEpic(Issue issue)
    {
        if (issue.Type == IssueType.Epic)
        {
            return CreateEpicSummary(issue);
        }

        if (issue.ParentIssue?.Type == IssueType.Epic)
        {
            return CreateEpicSummary(issue.ParentIssue);
        }

        if (issue.ParentIssue?.ParentIssue?.Type == IssueType.Epic)
        {
            return CreateEpicSummary(issue.ParentIssue.ParentIssue);
        }

        return null;
    }

    private static EpicSummary CreateEpicSummary(Issue epic)
    {
        return new EpicSummary(epic.Id, epic.IssueKey, epic.Title, ResolveEpicColor(epic.Id));
    }

    private static string ResolveEpicColor(int epicId)
    {
        var index = Math.Abs(epicId) % EpicColors.Length;
        return EpicColors[index];
    }

    private static TransitionMetadata? TryParseTransitionMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TransitionMetadata>(metadataJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record EpicSummary(int Id, string IssueKey, string Title, string Color);

    private sealed record TransitionMetadata(
        int OldStatusId,
        string OldStatusName,
        StatusCategory OldCategory,
        int NewStatusId,
        string NewStatusName,
        StatusCategory NewCategory);
}
