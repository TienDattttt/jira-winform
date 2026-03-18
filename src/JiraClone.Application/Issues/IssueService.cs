using System.ComponentModel.DataAnnotations;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Common;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Issues;

public class IssueService
{
    private readonly IIssueRepository _issues;
    private readonly IUserRepository _users;
    private readonly IProjectRepository _projects;
    private readonly ICommentRepository _comments;
    private readonly IAttachmentRepository _attachments;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IWorkflowService _workflowService;
    private readonly IWorkflowRepository _workflows;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IssueService> _logger;

    public IssueService(
        IIssueRepository issues,
        IUserRepository users,
        IProjectRepository projects,
        ICommentRepository comments,
        IAttachmentRepository attachments,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        IWorkflowService workflowService,
        IWorkflowRepository workflows,
        IUnitOfWork unitOfWork,
        ILogger<IssueService>? logger = null)
    {
        _issues = issues;
        _users = users;
        _projects = projects;
        _comments = comments;
        _attachments = attachments;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _workflowService = workflowService;
        _workflows = workflows;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<IssueService>.Instance;
    }

    public async Task<Issue> CreateAsync(IssueEditModel model, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);
        ValidateModel(model);

        var project = await _projects.GetByIdAsync(model.ProjectId, cancellationToken);
        if (project is null)
        {
            throw new ValidationException($"Project with id {model.ProjectId} was not found.");
        }

        var workflowStatus = await ResolveWorkflowStatusAsync(model.ProjectId, model.WorkflowStatusId, cancellationToken);
        var parentIssue = await ValidateParentRelationshipAsync(model.ProjectId, model.Type, model.ParentIssueId, issueId: null, cancellationToken);
        var descriptionText = MarkdownHtmlRenderer.Normalize(model.DescriptionText);
        var issue = new Issue
        {
            ProjectId = model.ProjectId,
            IssueKey = await GenerateIssueKeyAsync(project, cancellationToken),
            Title = model.Title.Trim(),
            DescriptionText = descriptionText,
            DescriptionHtml = MarkdownHtmlRenderer.Render(descriptionText),
            Type = model.Type,
            Priority = model.Priority,
            ReporterId = model.ReporterId,
            CreatedById = model.CreatedById,
            EstimateHours = model.EstimateHours,
            TimeSpentHours = model.TimeSpentHours,
            TimeRemainingHours = model.TimeRemainingHours,
            StoryPoints = model.StoryPoints,
            DueDate = model.DueDate,
            SprintId = model.SprintId,
            ParentIssueId = parentIssue?.Id,
            ParentIssue = parentIssue,
            WorkflowStatus = workflowStatus
        };

        issue.MoveTo(workflowStatus.Id, await _issues.GetNextBoardPositionAsync(model.ProjectId, workflowStatus.Id, cancellationToken));
        issue.Assignees = await BuildAssigneesAsync(model.AssigneeIds, issue, cancellationToken);

        await _issues.AddAsync(issue, cancellationToken);
        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = model.ProjectId,
            Issue = issue,
            UserId = model.CreatedById,
            ActionType = ActivityActionType.Created,
            NewValue = issue.Title
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return issue;
    }

    public async Task<Issue?> UpdateAsync(IssueEditModel model, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);
        if (model.Id is null)
        {
            throw new ValidationException("Issue id is required.");
        }

        ValidateModel(model);
        var issue = await _issues.GetByIdAsync(model.Id.Value, cancellationToken);
        if (issue is null)
        {
            throw new NotFoundException($"Issue with id {model.Id.Value} was not found.");
        }

        var parentIssue = await ValidateParentRelationshipAsync(issue.ProjectId, model.Type, model.ParentIssueId, issue.Id, cancellationToken);
        var originalStatusId = issue.WorkflowStatusId;
        var previousTitle = issue.Title;
        var previousDueDate = issue.DueDate;
        var descriptionText = MarkdownHtmlRenderer.Normalize(model.DescriptionText);
        issue.Title = model.Title.Trim();
        issue.DescriptionText = descriptionText;
        issue.DescriptionHtml = MarkdownHtmlRenderer.Render(descriptionText);
        issue.Type = model.Type;
        issue.Priority = model.Priority;
        issue.ReporterId = model.ReporterId;
        issue.EstimateHours = model.EstimateHours;
        issue.TimeSpentHours = model.TimeSpentHours;
        issue.TimeRemainingHours = model.TimeRemainingHours;
        issue.StoryPoints = model.StoryPoints;
        issue.DueDate = model.DueDate;
        issue.SprintId = model.SprintId;
        issue.ParentIssueId = parentIssue?.Id;
        issue.ParentIssue = parentIssue;
        issue.Assignees.Clear();
        issue.Assignees = await BuildAssigneesAsync(model.AssigneeIds, issue, cancellationToken);

        if (previousDueDate != model.DueDate)
        {
            await AddDueDateActivityAsync(issue, previousDueDate, model.DueDate, model.CreatedById, cancellationToken);
        }

        var requestedStatus = await ResolveWorkflowStatusAsync(issue.ProjectId, model.WorkflowStatusId ?? issue.WorkflowStatusId, cancellationToken);
        if (requestedStatus.Id != originalStatusId)
        {
            await _workflowService.ExecuteTransitionAsync(issue.Id, requestedStatus.Id, model.CreatedById, cancellationToken: cancellationToken);
        }
        else
        {
            issue.UpdatedAtUtc = DateTime.UtcNow;
            await _activityLogs.AddAsync(new ActivityLogEntity
            {
                ProjectId = issue.ProjectId,
                IssueId = issue.Id,
                UserId = model.CreatedById,
                ActionType = ActivityActionType.Updated,
                OldValue = previousTitle,
                NewValue = issue.Title
            }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return issue;
    }

    public Task<bool> MoveAsync(int issueId, int targetStatusId, decimal boardPosition, int userId, CancellationToken cancellationToken = default)
        => UpdateStatusAsync(issueId, targetStatusId, boardPosition, userId, cancellationToken);

    public async Task<bool> UpdateStatusAsync(int issueId, int targetStatusId, decimal boardPosition, int userId, CancellationToken cancellationToken = default)
    {
        var result = await _workflowService.ExecuteTransitionAsync(issueId, targetStatusId, userId, boardPosition, cancellationToken);
        return result.Succeeded;
    }

    public async Task<Issue?> UpdateDueDateAsync(int issueId, DateOnly? dueDate, int userId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);

        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return null;
        }

        if (issue.DueDate == dueDate)
        {
            return issue;
        }

        var previousDueDate = issue.DueDate;
        issue.DueDate = dueDate;
        issue.UpdatedAtUtc = DateTime.UtcNow;

        await AddDueDateActivityAsync(issue, previousDueDate, dueDate, userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated due date for issue {IssueId} from {PreviousDueDate} to {NextDueDate}.",
            issueId,
            previousDueDate,
            dueDate);

        return issue;
    }

    public async Task<Issue?> UpdateParentAsync(int issueId, int? parentIssueId, int userId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);

        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return null;
        }

        if (issue.ParentIssueId == parentIssueId)
        {
            return issue;
        }

        var previousParent = issue.ParentIssue;
        var nextParent = await ValidateParentRelationshipAsync(issue.ProjectId, issue.Type, parentIssueId, issue.Id, cancellationToken);
        issue.ParentIssueId = nextParent?.Id;
        issue.ParentIssue = nextParent;
        issue.UpdatedAtUtc = DateTime.UtcNow;

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = issue.ProjectId,
            IssueId = issue.Id,
            UserId = userId,
            ActionType = ActivityActionType.Updated,
            FieldName = issue.Type == IssueType.Subtask ? "Parent" : "Epic link",
            OldValue = previousParent?.IssueKey,
            NewValue = nextParent?.IssueKey ?? "Cleared"
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return issue;
    }

    public async Task<IssueDetailsDto?> GetDetailsAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return null;
        }

        var comments = await _comments.GetByIssueIdAsync(issueId, cancellationToken);
        var activity = await _activityLogs.GetIssueActivityAsync(issueId, cancellationToken);
        var attachments = await _attachments.GetByIssueIdAsync(issueId, cancellationToken);
        var subIssues = await _issues.GetSubIssuesAsync(issueId, cancellationToken);
        return new IssueDetailsDto(issue, comments, attachments, activity, subIssues);
    }

    public async Task<bool> DeleteAsync(int issueId, int? userId = null, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return false;
        }

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = issue.ProjectId,
            IssueId = issue.Id,
            UserId = userId ?? issue.CreatedById,
            ActionType = ActivityActionType.Deleted,
            OldValue = issue.Title
        }, cancellationToken);

        await _issues.RemoveAsync(issue, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidateModel(IssueEditModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            throw new ValidationException("Title is required.");
        }

        if (model.StoryPoints < 0)
        {
            throw new ValidationException("Story points cannot be negative.");
        }

        if (model.Type == IssueType.Subtask && !model.ParentIssueId.HasValue)
        {
            throw new ValidationException("Subtask must have a parent issue.");
        }

        if (model.Type == IssueType.Epic && model.ParentIssueId.HasValue)
        {
            throw new ValidationException("Epic cannot have a parent issue.");
        }
    }

    private async Task<string> GenerateIssueKeyAsync(Project project, CancellationToken cancellationToken)
    {
        var prefix = $"{project.Key.Trim().ToUpperInvariant()}-";
        var nextSequence = await _issues.GetNextIssueSequenceAsync(project.Id, cancellationToken);
        return $"{prefix}{nextSequence}";
    }

    private async Task<WorkflowStatus> ResolveWorkflowStatusAsync(int projectId, int? workflowStatusId, CancellationToken cancellationToken)
    {
        if (workflowStatusId.HasValue)
        {
            var selectedStatus = await _workflows.GetStatusByIdAsync(workflowStatusId.Value, cancellationToken);
            if (selectedStatus is null || selectedStatus.WorkflowDefinition.ProjectId != projectId)
            {
                throw new ValidationException("The selected workflow status was not found in this project.");
            }

            return selectedStatus;
        }

        var workflow = await _workflows.GetDefaultByProjectAsync(projectId, cancellationToken)
            ?? throw new ValidationException("The project does not have a default workflow.");
        var defaultStatus = workflow.Statuses
            .OrderBy(x => x.Category)
            .ThenBy(x => x.DisplayOrder)
            .FirstOrDefault();
        return defaultStatus ?? throw new ValidationException("The project workflow does not contain any statuses.");
    }

    private async Task<Issue?> ValidateParentRelationshipAsync(int projectId, IssueType childType, int? parentIssueId, int? issueId, CancellationToken cancellationToken)
    {
        if (!parentIssueId.HasValue)
        {
            return null;
        }

        if (issueId.HasValue && parentIssueId.Value == issueId.Value)
        {
            throw new ValidationException("An issue cannot be linked to itself.");
        }

        var parent = await _issues.GetByIdAsync(parentIssueId.Value, cancellationToken);
        if (parent is null)
        {
            throw new ValidationException($"Parent issue with id {parentIssueId.Value} was not found.");
        }

        if (parent.ProjectId != projectId)
        {
            throw new ValidationException("The selected parent issue belongs to a different project.");
        }

        if (childType == IssueType.Subtask)
        {
            if (parent.Type is IssueType.Subtask or IssueType.Epic)
            {
                throw new ValidationException("Subtasks can only be created under stories, tasks, or bugs.");
            }

            return parent;
        }

        if (childType is IssueType.Story or IssueType.Task)
        {
            if (parent.Type != IssueType.Epic)
            {
                throw new ValidationException("Only epics can be selected for Epic Link.");
            }

            return parent;
        }

        throw new ValidationException($"{childType} cannot be linked to a parent issue.");
    }

    private async Task<ICollection<IssueAssignee>> BuildAssigneesAsync(IEnumerable<int> assigneeIds, Issue issue, CancellationToken cancellationToken)
    {
        var result = new List<IssueAssignee>();
        foreach (var assigneeId in assigneeIds.Distinct())
        {
            var user = await _users.GetByIdAsync(assigneeId, cancellationToken);
            if (user is null)
            {
                continue;
            }

            result.Add(new IssueAssignee
            {
                Issue = issue,
                UserId = assigneeId,
                User = user,
                AssignedAtUtc = DateTime.UtcNow
            });
        }

        return result;
    }

    private async Task AddDueDateActivityAsync(Issue issue, DateOnly? previousDueDate, DateOnly? nextDueDate, int userId, CancellationToken cancellationToken)
    {
        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = issue.ProjectId,
            IssueId = issue.Id,
            UserId = userId,
            ActionType = ActivityActionType.Updated,
            FieldName = "Due date",
            OldValue = FormatDueDateActivity(previousDueDate),
            NewValue = nextDueDate.HasValue
                ? $"Due date set to {nextDueDate.Value:yyyy-MM-dd}"
                : "Due date cleared"
        }, cancellationToken);
    }

    private static string? FormatDueDateActivity(DateOnly? dueDate) =>
        dueDate.HasValue ? dueDate.Value.ToString("yyyy-MM-dd") : null;
}
