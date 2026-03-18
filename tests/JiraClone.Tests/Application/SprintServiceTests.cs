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
        var service = CreateService(sprintRepository, issueRepository, workflows, activityLogs);

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

    private static SprintService CreateService(
        Mock<ISprintRepository>? sprintRepository = null,
        Mock<IIssueRepository>? issueRepository = null,
        Mock<IWorkflowRepository>? workflowRepository = null,
        Mock<IActivityLogRepository>? activityLogRepository = null,
        Mock<IAuthorizationService>? authorization = null,
        Mock<ICurrentUserContext>? currentUserContext = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        currentUserContext ??= new Mock<ICurrentUserContext>();
        currentUserContext.Setup(x => x.CurrentUser).Returns(new User { Id = 99, UserName = "admin", DisplayName = "Admin User", Email = "admin@example.com" });
        workflowRepository ??= new Mock<IWorkflowRepository>();
        workflowRepository.Setup(x => x.GetDefaultByProjectAsync(1, default)).ReturnsAsync(CreateWorkflowDefinition());

        return new SprintService(
            (sprintRepository ?? new Mock<ISprintRepository>()).Object,
            (issueRepository ?? new Mock<IIssueRepository>()).Object,
            workflowRepository.Object,
            (authorization ?? new Mock<IAuthorizationService>()).Object,
            (activityLogRepository ?? new Mock<IActivityLogRepository>()).Object,
            currentUserContext.Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }

    private static WorkflowDefinition CreateWorkflowDefinition(WorkflowStatus? backlog = null)
    {
        var workflow = new WorkflowDefinition { Id = 1, ProjectId = 1, Name = "Default", IsDefault = true };
        workflow.Statuses.Add(backlog ?? CreateStatus(1, "Backlog", StatusCategory.ToDo, workflow));
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
            Color = "#42526E",
            DisplayOrder = id
        };
    }

    private static Issue CreateIssue(int id, WorkflowStatus status)
    {
        var issue = new Issue { Id = id, ProjectId = 1, ReporterId = 3, CreatedById = 9, Title = "Issue" };
        issue.WorkflowStatus = status;
        issue.MoveTo(status.Id, 1m);
        return issue;
    }
}
