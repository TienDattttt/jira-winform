using JiraClone.Application.Abstractions;
using JiraClone.Application.Sprints;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Moq;

namespace JiraClone.Tests.Application;

public class SprintServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidData_SavesPlannedSprint()
    {
        var sprintRepository = new Mock<ISprintRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = CreateService(sprintRepository: sprintRepository, unitOfWork: unitOfWork);

        var sprint = await service.CreateAsync(1, "Sprint 1", "Goal", null, null);

        Assert.Equal(SprintState.Planned, sprint.State);
        sprintRepository.Verify(x => x.AddAsync(It.IsAny<Sprint>(), default), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task StartSprintAsync_NoActiveSprint_SetsStateAndStartDate()
    {
        var sprint = new Sprint { Id = 2, ProjectId = 1, Name = "Sprint 1", State = SprintState.Planned };
        var sprintRepository = new Mock<ISprintRepository>();
        sprintRepository.Setup(x => x.GetByIdAsync(2, default)).ReturnsAsync(sprint);
        sprintRepository.Setup(x => x.GetActiveByProjectIdAsync(1, default)).ReturnsAsync((Sprint?)null);
        var service = CreateService(sprintRepository: sprintRepository);

        var started = await service.StartSprintAsync(2);

        Assert.True(started);
        Assert.Equal(SprintState.Active, sprint.State);
        Assert.NotNull(sprint.StartDate);
    }

    [Fact]
    public async Task StartSprintAsync_ActiveSprintExists_ThrowsInvalidOperationException()
    {
        var sprint = new Sprint { Id = 2, ProjectId = 1, Name = "Sprint 2", State = SprintState.Planned };
        var activeSprint = new Sprint { Id = 1, ProjectId = 1, Name = "Sprint 1", State = SprintState.Active };
        var sprintRepository = new Mock<ISprintRepository>();
        sprintRepository.Setup(x => x.GetByIdAsync(2, default)).ReturnsAsync(sprint);
        sprintRepository.Setup(x => x.GetActiveByProjectIdAsync(1, default)).ReturnsAsync(activeSprint);
        var service = CreateService(sprintRepository: sprintRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartSprintAsync(2));
    }

    [Fact]
    public async Task AssignIssuesAsync_ValidIssues_WritesSprintAssignedActivity()
    {
        var sprint = new Sprint { Id = 2, ProjectId = 1, Name = "Sprint 1", State = SprintState.Planned };
        var issue = CreateIssue(5, CreateStatus(2, "Selected", StatusCategory.ToDo));
        issue.SprintId = null;
        var sprintRepository = new Mock<ISprintRepository>();
        sprintRepository.Setup(x => x.GetByIdAsync(2, default)).ReturnsAsync(sprint);
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(5, default)).ReturnsAsync(issue);
        var activityLogs = new Mock<IActivityLogRepository>();
        var service = CreateService(sprintRepository, issueRepository, activityLogRepository: activityLogs);

        var assigned = await service.AssignIssuesAsync(2, [5]);

        Assert.True(assigned);
        Assert.Equal(2, issue.SprintId);
        activityLogs.Verify(x => x.AddAsync(It.Is<ActivityLog>(log => log.ActionType == ActivityActionType.SprintAssigned && log.IssueId == 5), default), Times.Once);
    }

    [Fact]
    public async Task CloseSprintAsync_ActiveSprint_ClosesAndSetsEndDate()
    {
        var sprint = new Sprint { Id = 2, ProjectId = 1, Name = "Sprint 1", State = SprintState.Active };
        var sprintRepository = new Mock<ISprintRepository>();
        sprintRepository.Setup(x => x.GetByIdAsync(2, default)).ReturnsAsync(sprint);
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetIncompleteBySprintIdAsync(2, default)).ReturnsAsync(Array.Empty<Issue>());
        var activityLogs = new Mock<IActivityLogRepository>();
        var workflows = new Mock<IWorkflowRepository>();
        workflows.Setup(x => x.GetDefaultByProjectAsync(1, default)).ReturnsAsync(CreateWorkflowDefinition());
        var service = CreateService(sprintRepository, issueRepository, workflows, activityLogRepository: activityLogs);

        var closed = await service.CloseSprintAsync(2, null);

        Assert.True(closed);
        Assert.Equal(SprintState.Closed, sprint.State);
        Assert.NotNull(sprint.EndDate);
        activityLogs.Verify(x => x.AddAsync(It.Is<ActivityLog>(log => log.ActionType == ActivityActionType.SprintClosed), default), Times.Once);
    }

    [Fact]
    public async Task CloseSprintAsync_IncompleteIssues_MovesThemToBacklog()
    {
        var sprint = new Sprint { Id = 2, ProjectId = 1, Name = "Sprint 1", State = SprintState.Active };
        var backlog = CreateStatus(1, "Backlog", StatusCategory.ToDo);
        var issue = CreateIssue(5, CreateStatus(2, "Selected", StatusCategory.ToDo));
        issue.SprintId = 2;
        var sprintRepository = new Mock<ISprintRepository>();
        sprintRepository.Setup(x => x.GetByIdAsync(2, default)).ReturnsAsync(sprint);
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetIncompleteBySprintIdAsync(2, default)).ReturnsAsync([issue]);
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, backlog.Id, default)).ReturnsAsync(1m);
        var workflows = new Mock<IWorkflowRepository>();
        workflows.Setup(x => x.GetDefaultByProjectAsync(1, default)).ReturnsAsync(CreateWorkflowDefinition(backlog));
        var service = CreateService(sprintRepository, issueRepository, workflows);

        await service.CloseSprintAsync(2, null);

        Assert.Null(issue.SprintId);
        Assert.Equal(backlog.Id, issue.WorkflowStatusId);
    }

    [Fact]
    public async Task GetCfdDataAsync_ReturnsCountsPerStatusPerDay()
    {
        var backlog = CreateStatus(1, "Backlog", StatusCategory.ToDo);
        var inProgress = CreateStatus(2, "In Progress", StatusCategory.InProgress);
        var done = CreateStatus(3, "Done", StatusCategory.Done);
        var workflow = CreateWorkflowDefinition(backlog, inProgress, done);

        var issueOne = CreateIssue(1, done, storyPoints: 5, createdAtUtc: new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc));
        var issueTwo = CreateIssue(2, backlog, storyPoints: 3, createdAtUtc: new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc));

        var issues = new[] { issueOne, issueTwo };
        var activityLogs = new[]
        {
            CreateStatusChangedLog(issueOne.Id, new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc), "Backlog", "In Progress"),
            CreateStatusChangedLog(issueOne.Id, new DateTime(2026, 3, 3, 16, 0, 0, DateTimeKind.Utc), "In Progress", "Done")
        };

        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetProjectIssuesAsync(1, default)).ReturnsAsync(issues);
        var workflowRepository = new Mock<IWorkflowRepository>();
        workflowRepository.Setup(x => x.GetDefaultByProjectAsync(1, default)).ReturnsAsync(workflow);
        var activityLogRepository = new Mock<IActivityLogRepository>();
        activityLogRepository.Setup(x => x.GetProjectStatusChangesAsync(1, default)).ReturnsAsync(activityLogs);
        var service = CreateService(issueRepository: issueRepository, workflowRepository: workflowRepository, activityLogRepository: activityLogRepository);

        var result = await service.GetCfdDataAsync(
            1,
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, result.Single(x => x.Date == new DateOnly(2026, 3, 1) && x.Status == "Backlog").IssueCount);
        Assert.Equal(1, result.Single(x => x.Date == new DateOnly(2026, 3, 2) && x.Status == "Backlog").IssueCount);
        Assert.Equal(1, result.Single(x => x.Date == new DateOnly(2026, 3, 2) && x.Status == "In Progress").IssueCount);
        Assert.Equal(1, result.Single(x => x.Date == new DateOnly(2026, 3, 3) && x.Status == "Done").IssueCount);
        Assert.Equal(1, result.Single(x => x.Date == new DateOnly(2026, 3, 3) && x.Status == "Backlog").IssueCount);
    }

    [Fact]
    public async Task GetSprintReportAsync_ClosedSprint_SplitsCompletedCarriedAndRemoved()
    {
        var backlog = CreateStatus(1, "Backlog", StatusCategory.ToDo);
        var inProgress = CreateStatus(2, "In Progress", StatusCategory.InProgress);
        var done = CreateStatus(3, "Done", StatusCategory.Done);
        var workflow = CreateWorkflowDefinition(backlog, inProgress, done);

        var sprint = new Sprint
        {
            Id = 2,
            ProjectId = 1,
            Name = "Sprint 1",
            State = SprintState.Closed,
            StartDate = new DateOnly(2026, 3, 1),
            EndDate = new DateOnly(2026, 3, 5),
            ClosedAtUtc = new DateTime(2026, 3, 5, 18, 0, 0, DateTimeKind.Utc)
        };

        var completedIssue = CreateIssue(10, done, storyPoints: 5, createdAtUtc: new DateTime(2026, 2, 28, 8, 0, 0, DateTimeKind.Utc));
        completedIssue.SprintId = sprint.Id;
        var carriedIssue = CreateIssue(11, inProgress, storyPoints: 3, createdAtUtc: new DateTime(2026, 2, 28, 9, 0, 0, DateTimeKind.Utc));
        carriedIssue.SprintId = sprint.Id;
        var removedIssue = CreateIssue(12, backlog, storyPoints: 2, createdAtUtc: new DateTime(2026, 2, 28, 10, 0, 0, DateTimeKind.Utc));
        removedIssue.SprintId = null;

        var issues = new[] { completedIssue, carriedIssue, removedIssue };
        var activityLogs = new[]
        {
            CreateStatusChangedLog(completedIssue.Id, new DateTime(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc), "In Progress", "Done"),
            CreateSprintAssignedLog(removedIssue.Id, new DateTime(2026, 3, 3, 11, 0, 0, DateTimeKind.Utc), sprint.Name, "Backlog")
        };

        var sprintRepository = new Mock<ISprintRepository>();
        sprintRepository.Setup(x => x.GetByIdAsync(sprint.Id, default)).ReturnsAsync(sprint);
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetProjectIssuesAsync(1, default)).ReturnsAsync(issues);
        var workflowRepository = new Mock<IWorkflowRepository>();
        workflowRepository.Setup(x => x.GetDefaultByProjectAsync(1, default)).ReturnsAsync(workflow);
        var activityLogRepository = new Mock<IActivityLogRepository>();
        activityLogRepository.Setup(x => x.GetProjectActivityAsync(1, int.MaxValue, default)).ReturnsAsync(activityLogs);
        var service = CreateService(
            sprintRepository: sprintRepository,
            issueRepository: issueRepository,
            workflowRepository: workflowRepository,
            activityLogRepository: activityLogRepository);

        var report = await service.GetSprintReportAsync(sprint.Id);

        Assert.NotNull(report);
        Assert.Equal(10, report!.CommittedStoryPoints);
        Assert.Equal(5, report.CompletedStoryPoints);
        Assert.Equal(50d, report.CompletionPercentage);
        Assert.Single(report.CompletedWork);
        Assert.Equal(completedIssue.Id, report.CompletedWork[0].IssueId);
        Assert.Single(report.NotCompleted);
        Assert.Equal(carriedIssue.Id, report.NotCompleted[0].IssueId);
        Assert.Single(report.RemovedFromSprint);
        Assert.Equal(removedIssue.Id, report.RemovedFromSprint[0].IssueId);
    }

    private static SprintService CreateService(
        Mock<ISprintRepository>? sprintRepository = null,
        Mock<IIssueRepository>? issueRepository = null,
        Mock<IWorkflowRepository>? workflowRepository = null,
        Mock<IProjectRepository>? projectRepository = null,
        Mock<IUserRepository>? userRepository = null,
        Mock<INotificationService>? notificationService = null,
        Mock<IWebhookDispatcher>? webhookDispatcher = null,
        Mock<IActivityLogRepository>? activityLogRepository = null,
        Mock<IPermissionService>? permissionService = null,
        Mock<ICurrentUserContext>? currentUserContext = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        currentUserContext ??= new Mock<ICurrentUserContext>();
        currentUserContext.Setup(x => x.CurrentUser).Returns(new User { Id = 99, UserName = "admin", DisplayName = "Admin User", Email = "admin@example.com" });
        currentUserContext.Setup(x => x.RequireUserId()).Returns(99);

        workflowRepository ??= new Mock<IWorkflowRepository>();
        workflowRepository.Setup(x => x.GetDefaultByProjectAsync(1, default)).ReturnsAsync(CreateWorkflowDefinition());

        projectRepository ??= new Mock<IProjectRepository>();
        projectRepository.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(new Project { Id = 1, Key = "PROJ", Name = "Project" });

        userRepository ??= new Mock<IUserRepository>();
        userRepository.Setup(x => x.GetProjectUsersAsync(1, default)).ReturnsAsync(new[]
        {
            new User { Id = 99, UserName = "admin", DisplayName = "Admin User", Email = "admin@example.com" }
        });

        permissionService ??= new Mock<IPermissionService>();
        permissionService
            .Setup(x => x.HasPermissionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Permission>(), default))
            .ReturnsAsync(true);

        return new SprintService(
            (sprintRepository ?? new Mock<ISprintRepository>()).Object,
            (issueRepository ?? new Mock<IIssueRepository>()).Object,
            workflowRepository.Object,
            projectRepository.Object,
            userRepository.Object,
            (notificationService ?? new Mock<INotificationService>()).Object,
            (webhookDispatcher ?? new Mock<IWebhookDispatcher>()).Object,
            permissionService.Object,
            (activityLogRepository ?? new Mock<IActivityLogRepository>()).Object,
            currentUserContext.Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }

    private static WorkflowDefinition CreateWorkflowDefinition(params WorkflowStatus[] statuses)
    {
        var workflow = new WorkflowDefinition { Id = 1, ProjectId = 1, Name = "Default", IsDefault = true };
        var resolvedStatuses = statuses.Length == 0
            ? new[] { CreateStatus(1, "Backlog", StatusCategory.ToDo, workflow) }
            : statuses;

        foreach (var status in resolvedStatuses)
        {
            status.WorkflowDefinition = workflow;
            status.WorkflowDefinitionId = workflow.Id;
            workflow.Statuses.Add(status);
        }

        return workflow;
    }

    private static WorkflowStatus CreateStatus(int id, string name, StatusCategory category, WorkflowDefinition? workflow = null)
    {
        workflow ??= new WorkflowDefinition { Id = 1, ProjectId = 1, Name = "Default", IsDefault = true };
        return new WorkflowStatus
        {
            Id = id,
            WorkflowDefinitionId = workflow.Id,
            WorkflowDefinition = workflow,
            Name = name,
            Category = category,
            Color = category switch
            {
                StatusCategory.Done => "#36B37E",
                StatusCategory.InProgress => "#0052CC",
                _ => "#6B778C"
            },
            DisplayOrder = id
        };
    }

    private static Issue CreateIssue(int id, WorkflowStatus status, int? storyPoints = null, DateTime? createdAtUtc = null)
    {
        var issue = new Issue
        {
            Id = id,
            ProjectId = 1,
            ReporterId = 3,
            CreatedById = 9,
            IssueKey = $"PROJ-{id}",
            Title = $"Issue {id}",
            StoryPoints = storyPoints,
            CreatedAtUtc = createdAtUtc ?? new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc)
        };
        issue.WorkflowStatus = status;
        issue.MoveTo(status.Id, 1m);
        return issue;
    }

    private static ActivityLog CreateStatusChangedLog(int issueId, DateTime occurredAtUtc, string oldValue, string newValue)
    {
        return new ActivityLog
        {
            ProjectId = 1,
            IssueId = issueId,
            UserId = 99,
            ActionType = ActivityActionType.StatusChanged,
            OldValue = oldValue,
            NewValue = newValue,
            OccurredAtUtc = occurredAtUtc
        };
    }

    private static ActivityLog CreateSprintAssignedLog(int issueId, DateTime occurredAtUtc, string oldValue, string newValue)
    {
        return new ActivityLog
        {
            ProjectId = 1,
            IssueId = issueId,
            UserId = 99,
            ActionType = ActivityActionType.SprintAssigned,
            OldValue = oldValue,
            NewValue = newValue,
            OccurredAtUtc = occurredAtUtc
        };
    }
}


