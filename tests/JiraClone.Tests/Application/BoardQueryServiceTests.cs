using System.Text.Json;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Boards;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Moq;

namespace JiraClone.Tests.Application;

public class BoardQueryServiceTests
{
    [Fact]
    public async Task GetBoardAsync_MapsTotalIssueCountPerColumn()
    {
        var backlogStatus = new WorkflowStatus { Id = 1, Name = "Backlog", Color = "#42526E", Category = StatusCategory.ToDo, DisplayOrder = 1 };
        var inProgressStatus = new WorkflowStatus { Id = 2, Name = "In Progress", Color = "#0052CC", Category = StatusCategory.InProgress, DisplayOrder = 2 };
        var project = new Project
        {
            Id = 7,
            Key = "JIRA",
            Name = "Jira Clone",
            BoardColumns =
            [
                new BoardColumn { Id = 10, ProjectId = 7, Name = "Backlog", WorkflowStatusId = 1, WorkflowStatus = backlogStatus, DisplayOrder = 1, WipLimit = 0 },
                new BoardColumn { Id = 11, ProjectId = 7, Name = "In Progress", WorkflowStatusId = 2, WorkflowStatus = inProgressStatus, DisplayOrder = 2, WipLimit = 3 }
            ]
        };
        project.Members.Add(new ProjectMember { ProjectId = 7, UserId = 99, ProjectRole = ProjectRole.Developer });

        var issues = new List<Issue>
        {
            CreateIssue(1, "JIRA-1", "Backlog issue", backlogStatus, 1m),
            CreateIssue(2, "JIRA-2", "Working issue", inProgressStatus, 1m),
            CreateIssue(3, "JIRA-3", "Working issue 2", inProgressStatus, 2m)
        };

        var issueRepository = new Mock<IIssueRepository>();
        var projectRepository = new Mock<IProjectRepository>();
        var activityLogs = new Mock<IActivityLogRepository>();
        var currentUserContext = new Mock<ICurrentUserContext>();
        var permissionService = new Mock<IPermissionService>();

        issueRepository.Setup(x => x.GetBoardIssuesAsync(7, null, default)).ReturnsAsync(issues);
        projectRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(project);
        currentUserContext.Setup(x => x.RequireUserId()).Returns(99);
        permissionService.Setup(x => x.HasPermissionAsync(99, 7, Permission.ViewProject, default)).ReturnsAsync(true);

        var service = new BoardQueryService(
            issueRepository.Object,
            projectRepository.Object,
            activityLogs.Object,
            currentUserContext.Object,
            permissionService.Object);

        var board = await service.GetBoardAsync(7, sprintId: null);

        Assert.Equal(2, board.Count);
        Assert.Equal(1, board[0].TotalIssueCount);
        Assert.Equal(2, board[1].TotalIssueCount);
        Assert.Equal(3, board[1].WipLimit);
    }

    [Fact]
    public async Task GetAverageCycleTimeAsync_StatusChanges_ReturnsAverageDuration()
    {
        var issueRepository = new Mock<IIssueRepository>();
        var projectRepository = new Mock<IProjectRepository>();
        var activityLogs = new Mock<IActivityLogRepository>();
        var currentUserContext = new Mock<ICurrentUserContext>();
        var permissionService = new Mock<IPermissionService>();

        currentUserContext.Setup(x => x.RequireUserId()).Returns(99);
        permissionService.Setup(x => x.HasPermissionAsync(99, 7, Permission.ViewProject, default)).ReturnsAsync(true);
        activityLogs.Setup(x => x.GetProjectStatusChangesAsync(7, default)).ReturnsAsync(
        [
            CreateStatusChange(1, 1, new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc), StatusCategory.ToDo, StatusCategory.InProgress),
            CreateStatusChange(2, 1, new DateTime(2026, 3, 3, 8, 0, 0, DateTimeKind.Utc), StatusCategory.InProgress, StatusCategory.Done),
            CreateStatusChange(3, 2, new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc), StatusCategory.ToDo, StatusCategory.InProgress),
            CreateStatusChange(4, 2, new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc), StatusCategory.InProgress, StatusCategory.Done)
        ]);

        var service = new BoardQueryService(
            issueRepository.Object,
            projectRepository.Object,
            activityLogs.Object,
            currentUserContext.Object,
            permissionService.Object);

        var average = await service.GetAverageCycleTimeAsync(7);

        Assert.NotNull(average);
        Assert.Equal(TimeSpan.FromDays(2.5), average!.Value);
    }

    private static Issue CreateIssue(int id, string issueKey, string title, WorkflowStatus status, decimal boardPosition)
    {
        var reporter = new User { Id = 99, UserName = "admin", DisplayName = "Admin", Email = "admin@example.com" };
        var issue = new Issue
        {
            Id = id,
            ProjectId = 7,
            IssueKey = issueKey,
            Title = title,
            Type = IssueType.Task,
            Priority = IssuePriority.Medium,
            WorkflowStatus = status,
            ReporterId = reporter.Id,
            Reporter = reporter,
            CreatedById = reporter.Id,
            Assignees = []
        };
        issue.MoveTo(status.Id, boardPosition);
        return issue;
    }

    private static ActivityLog CreateStatusChange(int id, int issueId, DateTime occurredAtUtc, StatusCategory oldCategory, StatusCategory newCategory)
    {
        return new ActivityLog
        {
            Id = id,
            ProjectId = 7,
            IssueId = issueId,
            UserId = 99,
            ActionType = ActivityActionType.StatusChanged,
            FieldName = nameof(Issue.WorkflowStatusId),
            OldValue = oldCategory.ToString(),
            NewValue = newCategory.ToString(),
            OccurredAtUtc = occurredAtUtc,
            MetadataJson = JsonSerializer.Serialize(new { OldStatusId = 1, OldStatusName = "From", OldCategory = oldCategory, NewStatusId = 2, NewStatusName = "To", NewCategory = newCategory })
        };
    }
}
