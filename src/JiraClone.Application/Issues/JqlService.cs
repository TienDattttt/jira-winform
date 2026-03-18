using JiraClone.Application.Abstractions;
using JiraClone.Application.Jql;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Issues;

public class JqlService : IJqlService
{
    private readonly IIssueRepository _issues;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly JqlParser _parser = new();
    private readonly JqlToLinqTranslator _translator = new();
    private readonly ILogger<JqlService> _logger;

    public JqlService(IIssueRepository issues, ICurrentUserContext currentUserContext, ILogger<JqlService>? logger = null)
    {
        _issues = issues;
        _currentUserContext = currentUserContext;
        _logger = logger ?? NullLogger<JqlService>.Instance;
    }

    public JqlQuery Parse(string? jql)
    {
        _logger.LogDebug("Parsing JQL query.");
        return _parser.Parse(jql);
    }

    public async Task<IReadOnlyList<IssueDto>> ExecuteQueryAsync(string? jql, int projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing JQL query for project {ProjectId}.", projectId);
        var parsed = Parse(jql);
        var currentUser = _currentUserContext.CurrentUser ?? throw new InvalidOperationException("A logged in user is required to execute JQL.");
        var context = new JqlTranslationContext(projectId, currentUser.Id, currentUser.UserName, currentUser.DisplayName, DateTime.UtcNow);
        var issues = await _issues.ExecuteQueryAsync(projectId, query => _translator.Apply(query, parsed, context), cancellationToken);
        _logger.LogInformation("JQL query for project {ProjectId} returned {IssueCount} issues.", projectId, issues.Count);
        return issues.Select(Map).ToList();
    }

    private static IssueDto Map(Issue issue)
    {
        return new IssueDto(
            issue.Id,
            issue.IssueKey,
            issue.Title,
            issue.Type,
            issue.Priority,
            issue.WorkflowStatusId,
            issue.WorkflowStatus.Name,
            issue.WorkflowStatus.Color,
            issue.WorkflowStatus.Category,
            issue.Project.Key,
            issue.Project.Name,
            issue.ReporterId,
            issue.Reporter.DisplayName,
            issue.Assignees.OrderBy(x => x.User.DisplayName).Select(x => x.User.DisplayName).ToList(),
            issue.SprintId,
            issue.Sprint?.Name,
            issue.CreatedAtUtc,
            issue.UpdatedAtUtc,
            issue.DueDate,
            issue.StoryPoints,
            issue.IssueLabels.OrderBy(x => x.Label.Name).Select(x => x.Label.Name).ToList(),
            issue.IssueComponents.OrderBy(x => x.Component.Name).Select(x => x.Component.Name).ToList());
    }
}
