using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Roadmap;

public sealed class RoadmapService : IRoadmapService
{
    private readonly IIssueRepository _issues;
    private readonly IProjectRepository _projects;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<RoadmapService> _logger;

    public RoadmapService(
        IIssueRepository issues,
        IProjectRepository projects,
        ICurrentUserContext currentUserContext,
        IPermissionService permissionService,
        ILogger<RoadmapService>? logger = null)
    {
        _issues = issues;
        _projects = projects;
        _currentUserContext = currentUserContext;
        _permissionService = permissionService;
        _logger = logger ?? NullLogger<RoadmapService>.Instance;
    }

    public async Task<IReadOnlyList<RoadmapEpicDto>> GetEpicsForRoadmapAsync(int projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading roadmap epics for project {ProjectId}.", projectId);
        await EnsurePermissionAsync(projectId, cancellationToken);

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            _logger.LogWarning("Roadmap request skipped because project {ProjectId} was not found.", projectId);
            return [];
        }

        var issues = await _issues.GetProjectIssuesAsync(projectId, cancellationToken);
        var childIssuesByEpicId = issues
            .Where(issue => issue.ParentIssueId.HasValue && !issue.IsDeleted)
            .GroupBy(issue => issue.ParentIssueId!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Issue>)group.OrderBy(issue => issue.Title).ToList());

        return issues
            .Where(issue => issue.Type == IssueType.Epic && !issue.IsDeleted)
            .Select(epic => MapEpic(epic, childIssuesByEpicId.GetValueOrDefault(epic.Id, [])))
            .OrderBy(epic => epic.StartDate ?? epic.DueDate ?? DateOnly.MaxValue)
            .ThenBy(epic => epic.DueDate ?? DateOnly.MaxValue)
            .ThenBy(epic => epic.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task EnsurePermissionAsync(int projectId, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserContext.RequireUserId();
        var canView = await _permissionService.HasPermissionAsync(currentUserId, projectId, Permission.ViewProject, cancellationToken);
        if (!canView)
        {
            throw new UnauthorizedAccessException("You do not have permission to view this roadmap.");
        }
    }

    private static RoadmapEpicDto MapEpic(Issue epic, IReadOnlyList<Issue> childIssues)
    {
        var epicAssignee = epic.Assignees
            .OrderBy(assignee => assignee.AssignedAtUtc)
            .Select(assignee => assignee.User)
            .FirstOrDefault();

        var doneIssues = childIssues.Where(issue => issue.WorkflowStatus.Category == StatusCategory.Done).ToList();
        var sprintIds = childIssues
            .Select(issue => issue.SprintId)
            .Append(epic.SprintId)
            .Where(sprintId => sprintId.HasValue)
            .Select(sprintId => sprintId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        var totalStoryPoints = childIssues.Sum(issue => Math.Max(issue.StoryPoints ?? 0, 0));
        var doneStoryPoints = doneIssues.Sum(issue => Math.Max(issue.StoryPoints ?? 0, 0));

        return new RoadmapEpicDto(
            epic.Id,
            epic.IssueKey,
            epic.Title,
            epic.StartDate,
            epic.DueDate,
            epic.WorkflowStatus.Name,
            epic.WorkflowStatus.Category,
            ResolveEpicColor(epic),
            epicAssignee?.Id,
            epicAssignee?.DisplayName,
            childIssues.Count,
            doneIssues.Count,
            totalStoryPoints,
            doneStoryPoints,
            sprintIds);
    }

    private static string ResolveEpicColor(Issue epic)
    {
        if (!string.IsNullOrWhiteSpace(epic.WorkflowStatus.Color))
        {
            return epic.WorkflowStatus.Color;
        }

        return epic.WorkflowStatus.Category switch
        {
            StatusCategory.Done => "#1F845A",
            StatusCategory.InProgress => "#0052CC",
            _ => "#6B778C"
        };
    }
}