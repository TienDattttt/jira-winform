using JiraClone.Application.Abstractions;
using JiraClone.Application.Common;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

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
    private readonly IUnitOfWork _unitOfWork;

    public IssueService(
        IIssueRepository issues,
        IUserRepository users,
        IProjectRepository projects,
        ICommentRepository comments,
        IAttachmentRepository attachments,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        IUnitOfWork unitOfWork)
    {
        _issues = issues;
        _users = users;
        _projects = projects;
        _comments = comments;
        _attachments = attachments;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _unitOfWork = unitOfWork;
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

        var issue = new Issue
        {
            ProjectId = model.ProjectId,
            IssueKey = await GenerateIssueKeyAsync(project, cancellationToken),
            Title = model.Title.Trim(),
            DescriptionText = model.DescriptionText,
            DescriptionHtml = model.DescriptionText,
            Type = model.Type,
            Priority = model.Priority,
            ReporterId = model.ReporterId,
            CreatedById = model.CreatedById,
            EstimateHours = model.EstimateHours,
            TimeSpentHours = model.TimeSpentHours,
            TimeRemainingHours = model.TimeRemainingHours,
            StoryPoints = model.StoryPoints,
            SprintId = model.SprintId,
            ParentIssueId = model.ParentIssueId
        };

        if (model.ParentIssueId.HasValue)
        {
            var parent = await _issues.GetByIdAsync(model.ParentIssueId.Value, cancellationToken);
            if (parent is null)
                throw new ValidationException($"Parent issue with id {model.ParentIssueId.Value} was not found.");
            if (parent.Type == IssueType.Subtask)
                throw new ValidationException("A subtask cannot be a parent of another issue.");
        }

        issue.MoveTo(model.Status, await _issues.GetNextBoardPositionAsync(model.ProjectId, model.Status, cancellationToken));
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

        var oldStatus = issue.Status;
        issue.Title = model.Title.Trim();
        issue.DescriptionText = model.DescriptionText;
        issue.DescriptionHtml = model.DescriptionText;
        issue.Type = model.Type;
        issue.Priority = model.Priority;
        issue.ReporterId = model.ReporterId;
        issue.EstimateHours = model.EstimateHours;
        issue.TimeSpentHours = model.TimeSpentHours;
        issue.TimeRemainingHours = model.TimeRemainingHours;
        issue.StoryPoints = model.StoryPoints;
        issue.SprintId = model.SprintId;
        issue.ParentIssueId = model.ParentIssueId;
        issue.Assignees.Clear();
        issue.Assignees = await BuildAssigneesAsync(model.AssigneeIds, issue, cancellationToken);

        if (oldStatus != model.Status)
        {
            var nextPosition = await _issues.GetNextBoardPositionAsync(issue.ProjectId, model.Status, cancellationToken);
            issue.MoveTo(model.Status, nextPosition);
        }
        else
        {
            issue.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = issue.ProjectId,
            IssueId = issue.Id,
            UserId = model.CreatedById,
            ActionType = oldStatus == model.Status ? ActivityActionType.Updated : ActivityActionType.StatusChanged,
            OldValue = oldStatus.ToString(),
            NewValue = issue.Status.ToString()
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return issue;
    }

    public Task<bool> MoveAsync(int issueId, IssueStatus targetStatus, decimal boardPosition, int userId, CancellationToken cancellationToken = default)
        => UpdateStatusAsync(issueId, targetStatus, boardPosition, userId, cancellationToken);

    public async Task<bool> UpdateStatusAsync(int issueId, IssueStatus targetStatus, decimal boardPosition, int userId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(Roles.RoleCatalog.Admin, Roles.RoleCatalog.ProjectManager, Roles.RoleCatalog.Developer);
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return false;
        }

        var oldStatus = issue.Status;
        issue.MoveTo(targetStatus, boardPosition);

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = issue.ProjectId,
            IssueId = issue.Id,
            UserId = userId,
            ActionType = ActivityActionType.StatusChanged,
            OldValue = oldStatus.ToString(),
            NewValue = targetStatus.ToString()
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
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
}

