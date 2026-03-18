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
    private readonly IPermissionService _permissionService;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IWorkflowService _workflowService;
    private readonly IWorkflowRepository _workflows;
    private readonly IWatcherRepository _watchers;
    private readonly INotificationService _notificationService;
    private readonly IWebhookDispatcher _webhookDispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IssueService> _logger;

    public IssueService(
        IIssueRepository issues,
        IUserRepository users,
        IProjectRepository projects,
        ICommentRepository comments,
        IAttachmentRepository attachments,
        IPermissionService permissionService,
        IActivityLogRepository activityLogs,
        IWorkflowService workflowService,
        IWorkflowRepository workflows,
        IWatcherRepository watchers,
        INotificationService notificationService,
        IWebhookDispatcher webhookDispatcher,
        IUnitOfWork unitOfWork,
        ILogger<IssueService>? logger = null)
    {
        _issues = issues;
        _users = users;
        _projects = projects;
        _comments = comments;
        _attachments = attachments;
        _permissionService = permissionService;
        _activityLogs = activityLogs;
        _workflowService = workflowService;
        _workflows = workflows;
        _watchers = watchers;
        _notificationService = notificationService;
        _webhookDispatcher = webhookDispatcher;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<IssueService>.Instance;
    }

    public async Task<Issue> CreateAsync(IssueEditModel model, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(model.CreatedById, model.ProjectId, Permission.CreateIssue, cancellationToken);
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

        await QueueAssignmentNotificationsAsync(issue, [], model.AssigneeIds, model.CreatedById, cancellationToken);
        await _webhookDispatcher.DispatchAsync(issue.ProjectId, WebhookEventType.IssueCreated, CreateIssueWebhookPayload(issue, model.CreatedById), cancellationToken);

        return issue;
    }

    public async Task<Issue?> UpdateAsync(IssueEditModel model, CancellationToken cancellationToken = default)
    {
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

        await EnsurePermissionAsync(model.CreatedById, issue.ProjectId, Permission.EditIssue, cancellationToken);

        var parentIssue = await ValidateParentRelationshipAsync(issue.ProjectId, model.Type, model.ParentIssueId, issue.Id, cancellationToken);
        var originalStatusId = issue.WorkflowStatusId;
        var previousTitle = issue.Title;
        var previousDueDate = issue.DueDate;
        var previousAssigneeIds = issue.Assignees.Select(x => x.UserId).ToHashSet();
        var nextAssigneeIds = model.AssigneeIds.Distinct().ToHashSet();
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
        issue.Assignees = await BuildAssigneesAsync(nextAssigneeIds, issue, cancellationToken);

        if (previousDueDate != model.DueDate)
        {
            await AddDueDateActivityAsync(issue, previousDueDate, model.DueDate, model.CreatedById, cancellationToken);
        }

        var requestedStatus = await ResolveWorkflowStatusAsync(issue.ProjectId, model.WorkflowStatusId ?? issue.WorkflowStatusId, cancellationToken);
        if (requestedStatus.Id != originalStatusId)
        {
            await ExecuteStatusTransitionAsync(issue.Id, requestedStatus.Id, issue.BoardPosition, model.CreatedById, cancellationToken);
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

        await QueueAssignmentNotificationsAsync(issue, previousAssigneeIds, nextAssigneeIds, model.CreatedById, cancellationToken);
        await _webhookDispatcher.DispatchAsync(issue.ProjectId, WebhookEventType.IssueUpdated, CreateIssueWebhookPayload(issue, model.CreatedById), cancellationToken);

        return issue;
    }

    public Task<bool> MoveAsync(int issueId, int targetStatusId, decimal boardPosition, int userId, CancellationToken cancellationToken = default)
        => UpdateStatusAsync(issueId, targetStatusId, boardPosition, userId, cancellationToken);

    public Task<bool> UpdateStatusAsync(int issueId, int targetStatusId, decimal boardPosition, int userId, CancellationToken cancellationToken = default)
        => ExecuteStatusTransitionAsync(issueId, targetStatusId, boardPosition, userId, cancellationToken);

    public async Task<Issue?> UpdateDueDateAsync(int issueId, DateOnly? dueDate, int userId, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return null;
        }
        await EnsurePermissionAsync(userId, issue.ProjectId, Permission.EditIssue, cancellationToken);
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
        await _webhookDispatcher.DispatchAsync(issue.ProjectId, WebhookEventType.IssueUpdated, CreateIssueWebhookPayload(issue, userId), cancellationToken);
        return issue;
    }
    public async Task<Issue?> UpdateScheduleAsync(int issueId, DateOnly? startDate, DateOnly? dueDate, int userId, CancellationToken cancellationToken = default)
    {
        if (startDate.HasValue && dueDate.HasValue && startDate.Value > dueDate.Value)
        {
            throw new ValidationException("Start date must be on or before the due date.");
        }
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return null;
        }
        await EnsurePermissionAsync(userId, issue.ProjectId, Permission.EditIssue, cancellationToken);
        if (issue.Type != IssueType.Epic)
        {
            throw new ValidationException("Roadmap scheduling is only available for epic issues.");
        }
        if (issue.StartDate == startDate && issue.DueDate == dueDate)
        {
            return issue;
        }
        var previousStartDate = issue.StartDate;
        var previousDueDate = issue.DueDate;
        issue.StartDate = startDate;
        issue.DueDate = dueDate;
        issue.UpdatedAtUtc = DateTime.UtcNow;
        if (previousStartDate != startDate)
        {
            await AddStartDateActivityAsync(issue, previousStartDate, startDate, userId, cancellationToken);
        }
        if (previousDueDate != dueDate)
        {
            await AddDueDateActivityAsync(issue, previousDueDate, dueDate, userId, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Updated roadmap schedule for epic {IssueId} from {PreviousStartDate}/{PreviousDueDate} to {NextStartDate}/{NextDueDate}.",
            issueId,
            previousStartDate,
            previousDueDate,
            startDate,
            dueDate);
        await _webhookDispatcher.DispatchAsync(issue.ProjectId, WebhookEventType.IssueUpdated, CreateIssueWebhookPayload(issue, userId), cancellationToken);
        return issue;
    }
    public async Task<Issue?> UpdateParentAsync(int issueId, int? parentIssueId, int userId, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return null;
        }

        await EnsurePermissionAsync(userId, issue.ProjectId, Permission.EditIssue, cancellationToken);

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
        await _webhookDispatcher.DispatchAsync(issue.ProjectId, WebhookEventType.IssueUpdated, CreateIssueWebhookPayload(issue, userId), cancellationToken);

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
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return false;
        }

        await EnsurePermissionAsync(userId ?? issue.CreatedById, issue.ProjectId, Permission.DeleteIssue, cancellationToken);

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
        await _webhookDispatcher.DispatchAsync(issue.ProjectId, WebhookEventType.IssueDeleted, CreateIssueWebhookPayload(issue, userId ?? issue.CreatedById), cancellationToken);
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

    private async Task AddStartDateActivityAsync(Issue issue, DateOnly? previousStartDate, DateOnly? nextStartDate, int userId, CancellationToken cancellationToken)
    {
        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = issue.ProjectId,
            IssueId = issue.Id,
            UserId = userId,
            ActionType = ActivityActionType.Updated,
            FieldName = "Start date",
            OldValue = FormatDateOnlyActivity(previousStartDate),
            NewValue = nextStartDate.HasValue
                ? $"Start date set to {nextStartDate.Value:yyyy-MM-dd}"
                : "Start date cleared"
        }, cancellationToken);
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
            OldValue = FormatDateOnlyActivity(previousDueDate),
            NewValue = nextDueDate.HasValue
                ? $"Due date set to {nextDueDate.Value:yyyy-MM-dd}"
                : "Due date cleared"
        }, cancellationToken);
    }
    private async Task<bool> ExecuteStatusTransitionAsync(int issueId, int targetStatusId, decimal? boardPosition, int userId, CancellationToken cancellationToken)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return false;
        }

        await EnsurePermissionAsync(userId, issue.ProjectId, Permission.TransitionIssue, cancellationToken);

        var result = await _workflowService.ExecuteTransitionAsync(issueId, targetStatusId, userId, boardPosition, cancellationToken);
        if (!result.Succeeded)
        {
            return false;
        }

        issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return false;
        }

        await QueueStatusChangeNotificationsAsync(issue, result.PreviousStatus, result.CurrentStatus, userId, cancellationToken);
        await _webhookDispatcher.DispatchAsync(issue.ProjectId, WebhookEventType.IssueStatusChanged, CreateIssueStatusChangeWebhookPayload(issue, result.PreviousStatus, result.CurrentStatus, userId), cancellationToken);

        return true;
    }

    private async Task EnsurePermissionAsync(int userId, int projectId, Permission permission, CancellationToken cancellationToken)
    {
        if (!await _permissionService.HasPermissionAsync(userId, projectId, permission, cancellationToken))
        {
            throw new UnauthorizedAccessException("Current user does not have permission to perform this action.");
        }
    }

    private async Task<bool> QueueAssignmentNotificationsAsync(Issue issue, IEnumerable<int> previousAssigneeIds, IEnumerable<int> nextAssigneeIds, int actorUserId, CancellationToken cancellationToken)
    {
        var recipients = nextAssigneeIds
            .Distinct()
            .Except(previousAssigneeIds.Distinct())
            .Where(userId => userId != actorUserId)
            .ToList();
        if (recipients.Count == 0)
        {
            return false;
        }

        var actor = await _users.GetByIdAsync(actorUserId, cancellationToken);
        var actorName = actor?.DisplayName ?? "Someone";
        foreach (var recipientUserId in recipients)
        {
            await _notificationService.CreateNotificationAsync(
                recipientUserId,
                NotificationType.IssueAssigned,
                $"Assigned to {issue.IssueKey}",
                $"{actorName} assigned you to {issue.IssueKey} - {issue.Title}.",
                issue.Id,
                issue.ProjectId,
                cancellationToken);
        }

        return true;
    }

    private async Task<bool> QueueStatusChangeNotificationsAsync(Issue issue, WorkflowStatus? previousStatus, WorkflowStatus? currentStatus, int actorUserId, CancellationToken cancellationToken)
    {
        if (currentStatus is null)
        {
            return false;
        }

        var watcherUserIds = (await _watchers.GetByIssueIdAsync(issue.Id, cancellationToken)).Select(x => x.UserId);
        var assigneeUserIds = issue.Assignees.Select(x => x.UserId);
        var recipients = watcherUserIds
            .Concat(assigneeUserIds)
            .Where(userId => userId != actorUserId)
            .Distinct()
            .ToList();
        if (recipients.Count == 0)
        {
            return false;
        }

        var actor = await _users.GetByIdAsync(actorUserId, cancellationToken);
        var actorName = actor?.DisplayName ?? "Someone";
        var transitionText = previousStatus is null
            ? currentStatus.Name
            : $"{previousStatus.Name} to {currentStatus.Name}";
        foreach (var recipientUserId in recipients)
        {
            await _notificationService.CreateNotificationAsync(
                recipientUserId,
                NotificationType.IssueStatusChanged,
                $"Status changed: {issue.IssueKey}",
                $"{actorName} moved {issue.IssueKey} from {transitionText}.",
                issue.Id,
                issue.ProjectId,
                cancellationToken);
        }

        return true;
    }

    private static object CreateIssueWebhookPayload(Issue issue, int actorUserId)
    {
        return new
        {
            issue.Id,
            issue.ProjectId,
            issue.IssueKey,
            issue.Title,
            issue.Type,
            issue.Priority,
            issue.ReporterId,
            issue.ParentIssueId,
            issue.SprintId,
            issue.StoryPoints,
            issue.StartDate,
            issue.DueDate,
            issue.WorkflowStatusId,
            WorkflowStatus = issue.WorkflowStatus.Name,
            WorkflowCategory = issue.WorkflowStatus.Category,
            AssigneeIds = issue.Assignees.Select(x => x.UserId).OrderBy(x => x).ToArray(),
            issue.IsDeleted,
            issue.CreatedAtUtc,
            issue.UpdatedAtUtc,
            ActorUserId = actorUserId,
        };
    }

    private static object CreateIssueStatusChangeWebhookPayload(Issue issue, WorkflowStatus? previousStatus, WorkflowStatus? currentStatus, int actorUserId)
    {
        return new
        {
            issue.Id,
            issue.ProjectId,
            issue.IssueKey,
            issue.Title,
            issue.Type,
            issue.Priority,
            issue.BoardPosition,
            issue.WorkflowStatusId,
            PreviousStatus = previousStatus is null ? null : new { previousStatus.Id, previousStatus.Name, previousStatus.Category },
            CurrentStatus = currentStatus is null ? null : new { currentStatus.Id, currentStatus.Name, currentStatus.Category },
            AssigneeIds = issue.Assignees.Select(x => x.UserId).OrderBy(x => x).ToArray(),
            issue.UpdatedAtUtc,
            ActorUserId = actorUserId,
        };
    }
    private static string? FormatDateOnlyActivity(DateOnly? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd") : null;
}






