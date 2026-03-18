using JiraClone.Application.Abstractions;
using JiraClone.Application.Roadmap;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Moq;

namespace JiraClone.Tests.Application;

public class RoadmapServiceTests
{
    [Fact]
    public async Task GetEpicsForRoadmapAsync_ReturnsEpicProgressAndSprintCoverage()
    {
        var epicStatus = new WorkflowStatus { Id = 11, Name = "In Progress", Color = "#0052CC", Category = StatusCategory.InProgress, DisplayOrder = 2 };
        var doneStatus = new WorkflowStatus { Id = 12, Name = "Done", Color = "#1F845A", Category = StatusCategory.Done, DisplayOrder = 3 };
        var todoStatus = new WorkflowStatus { Id = 13, Name = "Backlog", Color = "#6B778C", Category = StatusCategory.ToDo, DisplayOrder = 1 };
        var assignee = new User { Id = 7, UserName = "mai", DisplayName = "Mai Nguyen", Email = "mai@example.com" };

        var epic = new Issue
        {
            Id = 100,
            ProjectId = 1,
            IssueKey = "PROJ-100",
            Title = "Epic roadmap",
            Type = IssueType.Epic,
            StartDate = new DateOnly(2026, 3, 10),
            DueDate = new DateOnly(2026, 3, 24),
            WorkflowStatus = epicStatus,
            ReporterId = 1,
            Reporter = new User { Id = 1, UserName = "admin", DisplayName = "Admin", Email = "admin@example.com" },
            CreatedById = 1,
            Assignees =
            [
                new IssueAssignee { IssueId = 100, UserId = assignee.Id, User = assignee, AssignedAtUtc = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc) }
            ]
        };
        epic.MoveTo(epicStatus.Id, 1m);

        var childOne = CreateChildIssue(101, 100, todoStatus, 3, sprintId: 5);
        var childTwo = CreateChildIssue(102, 100, doneStatus, 5, sprintId: 6);
        var unrelatedEpic = new Issue
        {
            Id = 200,
            ProjectId = 1,
            IssueKey = "PROJ-200",
            Title = "Second epic",
            Type = IssueType.Epic,
            WorkflowStatus = todoStatus,
            ReporterId = 1,
            Reporter = epic.Reporter,
            CreatedById = 1,
            Assignees = []
        };
        unrelatedEpic.MoveTo(todoStatus.Id, 2m);

        var issues = new List<Issue> { epic, childOne, childTwo, unrelatedEpic };

        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetProjectIssuesAsync(1, default)).ReturnsAsync(issues);

        var projectRepository = new Mock<IProjectRepository>();
        projectRepository.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(new Project { Id = 1, Key = "PROJ", Name = "Project" });

        var currentUserContext = new Mock<ICurrentUserContext>();
        currentUserContext.Setup(x => x.RequireUserId()).Returns(99);

        var permissionService = new Mock<IPermissionService>();
        permissionService.Setup(x => x.HasPermissionAsync(99, 1, Permission.ViewProject, default)).ReturnsAsync(true);

        var service = new RoadmapService(issueRepository.Object, projectRepository.Object, currentUserContext.Object, permissionService.Object);

        var result = await service.GetEpicsForRoadmapAsync(1);

        Assert.Equal(2, result.Count);
        var mappedEpic = Assert.Single(result.Where(x => x.EpicId == epic.Id));
        Assert.Equal("PROJ-100", mappedEpic.IssueKey);
        Assert.Equal(2, mappedEpic.ChildIssueCount);
        Assert.Equal(1, mappedEpic.DoneCount);
        Assert.Equal(8, mappedEpic.TotalStoryPoints);
        Assert.Equal(5, mappedEpic.DoneStoryPoints);
        Assert.Equal(assignee.Id, mappedEpic.AssigneeId);
        Assert.Equal("Mai Nguyen", mappedEpic.AssigneeName);
        Assert.Equal([5, 6], mappedEpic.SprintIds);
    }

    private static Issue CreateChildIssue(int id, int epicId, WorkflowStatus status, int storyPoints, int? sprintId)
    {
        var reporter = new User { Id = 3, UserName = "user", DisplayName = "User", Email = "user@example.com" };
        var issue = new Issue
        {
            Id = id,
            ProjectId = 1,
            ParentIssueId = epicId,
            IssueKey = $"PROJ-{id}",
            Title = $"Child {id}",
            Type = IssueType.Story,
            StoryPoints = storyPoints,
            SprintId = sprintId,
            WorkflowStatus = status,
            ReporterId = reporter.Id,
            Reporter = reporter,
            CreatedById = reporter.Id,
            Assignees = []
        };
        issue.MoveTo(status.Id, id);
        return issue;
    }
}