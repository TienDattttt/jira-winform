using JiraClone.Application.Abstractions;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Labels;

public class LabelService : ILabelService
{
    private readonly ILabelRepository _labels;
    private readonly IIssueRepository _issues;
    private readonly IProjectRepository _projects;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LabelService> _logger;

    public LabelService(
        ILabelRepository labels,
        IIssueRepository issues,
        IProjectRepository projects,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork,
        ILogger<LabelService>? logger = null)
    {
        _labels = labels;
        _issues = issues;
        _projects = projects;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _currentUserContext = currentUserContext;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<LabelService>.Instance;
    }

    public Task<IReadOnlyList<Label>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading labels for project {ProjectId}.", projectId);
        return _labels.GetByProjectAsync(projectId, cancellationToken);
    }

    public async Task<Label> CreateAsync(int projectId, string name, string color, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating label in project {ProjectId}.", projectId);
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var project = await RequireProjectAsync(projectId, cancellationToken);
        var normalizedName = NormalizeName(name);
        var normalizedColor = NormalizeColor(color);
        EnsureUniqueLabel(project, normalizedName, null);

        var label = new Label
        {
            ProjectId = projectId,
            Name = normalizedName,
            Color = normalizedColor
        };

        await _labels.AddAsync(label, cancellationToken);
        await AddProjectActivityAsync(projectId, ActivityActionType.Created, nameof(Label.Name), null, label.Name, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return label;
    }

    public async Task<Label?> UpdateAsync(int labelId, string name, string color, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating label {LabelId}.", labelId);
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var label = await _labels.GetByIdAsync(labelId, cancellationToken);
        if (label is null)
        {
            _logger.LogWarning("Label {LabelId} was not found for update.", labelId);
            return null;
        }

        var project = await RequireProjectAsync(label.ProjectId, cancellationToken);
        var normalizedName = NormalizeName(name);
        var normalizedColor = NormalizeColor(color);
        EnsureUniqueLabel(project, normalizedName, label.Id);

        var previousValue = $"{label.Name}|{label.Color}";
        label.Name = normalizedName;
        label.Color = normalizedColor;
        label.UpdatedAtUtc = DateTime.UtcNow;

        await AddProjectActivityAsync(label.ProjectId, ActivityActionType.Updated, nameof(Label.Name), previousValue, $"{label.Name}|{label.Color}", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return label;
    }

    public async Task<bool> DeleteAsync(int labelId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting label {LabelId}.", labelId);
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        var label = await _labels.GetByIdAsync(labelId, cancellationToken);
        if (label is null)
        {
            _logger.LogWarning("Label {LabelId} was not found for delete.", labelId);
            return false;
        }

        await _labels.RemoveAsync(label, cancellationToken);
        await AddProjectActivityAsync(label.ProjectId, ActivityActionType.Deleted, nameof(Label.Name), label.Name, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AssignToIssueAsync(int issueId, IReadOnlyCollection<int> labelIds, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Assigning {LabelCount} labels to issue {IssueId}.", labelIds.Count, issueId);
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager, RoleCatalog.Developer);
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            _logger.LogWarning("Issue {IssueId} was not found for label assignment.", issueId);
            return false;
        }

        var projectLabels = await _labels.GetByProjectAsync(issue.ProjectId, cancellationToken);
        var selected = projectLabels.Where(x => labelIds.Contains(x.Id)).ToDictionary(x => x.Id);
        var previousValue = string.Join(", ", issue.IssueLabels.Select(x => x.Label.Name).OrderBy(x => x));

        var removed = issue.IssueLabels.Where(x => !selected.ContainsKey(x.LabelId)).ToList();
        foreach (var item in removed)
        {
            issue.IssueLabels.Remove(item);
        }

        var existingIds = issue.IssueLabels.Select(x => x.LabelId).ToHashSet();
        foreach (var label in selected.Values)
        {
            if (existingIds.Contains(label.Id))
            {
                continue;
            }

            issue.IssueLabels.Add(new IssueLabel
            {
                Issue = issue,
                IssueId = issue.Id,
                Label = label,
                LabelId = label.Id
            });
        }

        issue.UpdatedAtUtc = DateTime.UtcNow;
        var nextValue = string.Join(", ", issue.IssueLabels.Select(x => x.Label.Name).OrderBy(x => x));
        await AddIssueActivityAsync(issue, nameof(Label.Name), previousValue, nextValue, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Project> RequireProjectAsync(int projectId, CancellationToken cancellationToken)
    {
        return await _projects.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} was not found.");
    }

    private static void EnsureUniqueLabel(Project project, string normalizedName, int? currentLabelId)
    {
        var duplicate = project.Labels.Any(x => x.Id != currentLabelId && string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            throw new InvalidOperationException($"Label '{normalizedName}' already exists in this project.");
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Label name is required.");
        }

        return name.Trim();
    }

    private static string NormalizeColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return "#4688EC";
        }

        var normalized = color.Trim();
        return normalized.StartsWith('#') ? normalized : $"#{normalized}";
    }

    private async Task AddProjectActivityAsync(int projectId, ActivityActionType actionType, string fieldName, string? oldValue, string? newValue, CancellationToken cancellationToken)
    {
        if (_currentUserContext.CurrentUser?.Id is not int actorUserId)
        {
            return;
        }

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = projectId,
            UserId = actorUserId,
            ActionType = actionType,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue
        }, cancellationToken);
    }

    private async Task AddIssueActivityAsync(Issue issue, string fieldName, string? oldValue, string? newValue, CancellationToken cancellationToken)
    {
        if (_currentUserContext.CurrentUser?.Id is not int actorUserId)
        {
            return;
        }

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = issue.ProjectId,
            IssueId = issue.Id,
            UserId = actorUserId,
            ActionType = ActivityActionType.Updated,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue
        }, cancellationToken);
    }
}
