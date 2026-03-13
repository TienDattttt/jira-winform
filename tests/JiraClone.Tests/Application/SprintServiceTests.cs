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
        // Arrange
        var sprintRepository = new Mock<ISprintRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = CreateService(sprintRepository: sprintRepository, unitOfWork: unitOfWork);

        // Act
        var sprint = await service.CreateAsync(1, "Sprint 1", "Goal", null, null);

        // Assert
        Assert.Equal(SprintState.Planned, sprint.State);
        sprintRepository.Verify(x => x.AddAsync(It.IsAny<Sprint>(), default), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task StartSprintAsync_NoActiveSprint_SetsStateAndStartDate()
    {
        // Arrange
        var sprint = new Sprint { Id = 2, ProjectId = 1, Name = "Sprint 1", State = SprintState.Planned };
        var sprintRepository = new Mock<ISprintRepository>();
        sprintRepository.Setup(x => x.GetByIdAsync(2, default)).ReturnsAsync(sprint);
        sprintRepository.Setup(x => x.GetActiveByProjectIdAsync(1, default)).ReturnsAsync((Sprint?)null);
        var service = CreateService(sprintRepository: sprintRepository);

        // Act
        var started = await service.StartSprintAsync(2);

        // Assert
        Assert.True(started);
        Assert.Equal(SprintState.Active, sprint.State);
        Assert.NotNull(sprint.StartDate);
    }

    [Fact]
    public async Task StartSprintAsync_ActiveSprintExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var sprint = new Sprint { Id = 2, ProjectId = 1, Name = "Sprint 2", State = SprintState.Planned };
        var activeSprint = new Sprint { Id = 1, ProjectId = 1, Name = "Sprint 1", State = SprintState.Active };
        var sprintRepository = new Mock<ISprintRepository>();
        sprintRepository.Setup(x => x.GetByIdAsync(2, default)).ReturnsAsync(sprint);
        sprintRepository.Setup(x => x.GetActiveByProjectIdAsync(1, default)).ReturnsAsync(activeSprint);
        var service = CreateService(sprintRepository: sprintRepository);

        // Act
        var act = () => service.StartSprintAsync(2);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task CloseSprintAsync_ActiveSprint_ClosesAndSetsEndDate()
    {
        // Arrange
        var sprint = new Sprint { Id = 2, ProjectId = 1, Name = "Sprint 1", State = SprintState.Active };
        var sprintRepository = new Mock<ISprintRepository>();
        sprintRepository.Setup(x => x.GetByIdAsync(2, default)).ReturnsAsync(sprint);
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetIncompleteBySprintIdAsync(2, default)).ReturnsAsync(Array.Empty<Issue>());
        var service = CreateService(sprintRepository, issueRepository);

        // Act
        var closed = await service.CloseSprintAsync(2, null);

        // Assert
        Assert.True(closed);
        Assert.Equal(SprintState.Closed, sprint.State);
        Assert.NotNull(sprint.EndDate);
    }

    [Fact]
    public async Task CloseSprintAsync_IncompleteIssues_MovesThemToBacklog()
    {
        // Arrange
        var sprint = new Sprint { Id = 2, ProjectId = 1, Name = "Sprint 1", State = SprintState.Active };
        var issue = new Issue { Id = 5, ProjectId = 1, SprintId = 2, ReporterId = 3, CreatedById = 9, Title = "Open work" };
        var sprintRepository = new Mock<ISprintRepository>();
        sprintRepository.Setup(x => x.GetByIdAsync(2, default)).ReturnsAsync(sprint);
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetIncompleteBySprintIdAsync(2, default)).ReturnsAsync([issue]);
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, IssueStatus.Backlog, default)).ReturnsAsync(1m);
        var service = CreateService(sprintRepository, issueRepository);

        // Act
        await service.CloseSprintAsync(2, null);

        // Assert
        Assert.Null(issue.SprintId);
        Assert.Equal(IssueStatus.Backlog, issue.Status);
    }

    private static SprintService CreateService(
        Mock<ISprintRepository>? sprintRepository = null,
        Mock<IIssueRepository>? issueRepository = null,
        Mock<IAuthorizationService>? authorization = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        return new SprintService(
            (sprintRepository ?? new Mock<ISprintRepository>()).Object,
            (issueRepository ?? new Mock<IIssueRepository>()).Object,
            (authorization ?? new Mock<IAuthorizationService>()).Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }
}
