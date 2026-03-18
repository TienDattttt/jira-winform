using System.ComponentModel.DataAnnotations;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Common;
using JiraClone.Application.Issues;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Moq;

namespace JiraClone.Tests.Application;

public class IssueServiceTests
{
    private const int BacklogStatusId = 1;
    private const int SelectedStatusId = 2;
    private const int DoneStatusId = 4;

    [Fact]
    public async Task CreateAsync_ValidInput_SavesIssueAndReturnsEntity()
    {
        var issueRepository = new Mock<IIssueRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, SelectedStatusId, default)).ReturnsAsync(1m);
        issueRepository.Setup(x => x.GetNextIssueSequenceAsync(1, default)).ReturnsAsync(1);
        issueRepository.Setup(x => x.AddAsync(It.IsAny<Issue>(), default)).Callback<Issue, CancellationToken>((issue, _) => issue.Id = 42).Returns(Task.CompletedTask);
        var service = CreateService(issueRepository: issueRepository, unitOfWork: unitOfWork);

        var result = await service.CreateAsync(CreateModel(workflowStatusId: SelectedStatusId));

        Assert.Equal(42, result.Id);
        Assert.Equal("PROJ-1", result.IssueKey);
        Assert.Equal(SelectedStatusId, result.WorkflowStatusId);
        issueRepository.Verify(x => x.AddAsync(It.IsAny<Issue>(), default), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_RendersDescriptionHtmlFromMarkdown()
    {
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, BacklogStatusId, default)).ReturnsAsync(1m);
        issueRepository.Setup(x => x.GetNextIssueSequenceAsync(1, default)).ReturnsAsync(1);
        issueRepository.Setup(x => x.AddAsync(It.IsAny<Issue>(), default)).Returns(Task.CompletedTask);
        var service = CreateService(issueRepository: issueRepository);

        var result = await service.CreateAsync(CreateModel(descriptionText: "**Bold** item"));

        Assert.Equal("**Bold** item", result.DescriptionText);
        Assert.Contains("<strong>Bold</strong>", result.DescriptionHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_WhenDueDateProvided_SetsIssueDueDate()
    {
        var dueDate = new DateOnly(2026, 3, 27);
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, BacklogStatusId, default)).ReturnsAsync(1m);
        issueRepository.Setup(x => x.GetNextIssueSequenceAsync(1, default)).ReturnsAsync(1);
        issueRepository.Setup(x => x.AddAsync(It.IsAny<Issue>(), default)).Returns(Task.CompletedTask);
        var service = CreateService(issueRepository: issueRepository);

        var result = await service.CreateAsync(CreateModel(dueDate: dueDate));

        Assert.Equal(dueDate, result.DueDate);
    }

    [Fact]
    public async Task UpdateAsync_ValidInput_CallsSaveChanges()
    {
        var status = CreateStatus(BacklogStatusId, "Backlog", StatusCategory.ToDo);
        var issue = CreateIssue(7, status);
        var issueRepository = new Mock<IIssueRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(issue);
        var service = CreateService(issueRepository: issueRepository, unitOfWork: unitOfWork);

        await service.UpdateAsync(CreateModel(id: 7, title: "Updated", workflowStatusId: BacklogStatusId));

        Assert.Equal("Updated", issue.Title);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_RendersDescriptionHtmlFromMarkdown()
    {
        var status = CreateStatus(BacklogStatusId, "Backlog", StatusCategory.ToDo);
        var issue = CreateIssue(7, status);
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(issue);
        var service = CreateService(issueRepository: issueRepository);

        await service.UpdateAsync(CreateModel(id: 7, descriptionText: "`code` sample", workflowStatusId: BacklogStatusId));

        Assert.Equal("`code` sample", issue.DescriptionText);
        Assert.Contains("<code>code</code>", issue.DescriptionHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_WhenDueDateChanges_UpdatesIssueAndWritesActivityLog()
    {
        var status = CreateStatus(BacklogStatusId, "Backlog", StatusCategory.ToDo);
        var issue = CreateIssue(7, status);
        issue.DueDate = new DateOnly(2026, 3, 20);
        var issueRepository = new Mock<IIssueRepository>();
        var activityLogRepository = new Mock<IActivityLogRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(issue);
        var service = CreateService(issueRepository: issueRepository, activityLogRepository: activityLogRepository);

        await service.UpdateAsync(CreateModel(id: 7, workflowStatusId: BacklogStatusId, dueDate: new DateOnly(2026, 3, 24)));

        Assert.Equal(new DateOnly(2026, 3, 24), issue.DueDate);
        activityLogRepository.Verify(
            x => x.AddAsync(It.Is<ActivityLog>(log => log.FieldName == "Due date" && log.NewValue == "Due date set to 2026-03-24"), default),
            Times.Once);
    }

    [Fact]
    public async Task UpdateDueDateAsync_ValidInput_SavesIssueAndWritesActivityLog()
    {
        var status = CreateStatus(BacklogStatusId, "Backlog", StatusCategory.ToDo);
        var issue = CreateIssue(7, status);
        var issueRepository = new Mock<IIssueRepository>();
        var activityLogRepository = new Mock<IActivityLogRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(issue);
        var service = CreateService(issueRepository: issueRepository, activityLogRepository: activityLogRepository, unitOfWork: unitOfWork);

        var updated = await service.UpdateDueDateAsync(7, new DateOnly(2026, 3, 29), 11);

        Assert.NotNull(updated);
        Assert.Equal(new DateOnly(2026, 3, 29), issue.DueDate);
        activityLogRepository.Verify(
            x => x.AddAsync(It.Is<ActivityLog>(log => log.FieldName == "Due date" && log.NewValue == "Due date set to 2026-03-29" && log.UserId == 11), default),
            Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_IssueExists_SoftDeletesAndWritesActivityLog()
    {
        var status = CreateStatus(BacklogStatusId, "Backlog", StatusCategory.ToDo);
        var issue = CreateIssue(7, status);
        var issueRepository = new Mock<IIssueRepository>();
        var activityLogRepository = new Mock<IActivityLogRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(issue);
        issueRepository.Setup(x => x.RemoveAsync(issue, default)).Callback<Issue, CancellationToken>((entity, _) => entity.IsDeleted = true).Returns(Task.CompletedTask);
        var service = CreateService(issueRepository: issueRepository, activityLogRepository: activityLogRepository, unitOfWork: unitOfWork);

        var deleted = await service.DeleteAsync(7, 11);

        Assert.True(deleted);
        Assert.True(issue.IsDeleted);
        activityLogRepository.Verify(x => x.AddAsync(It.Is<ActivityLog>(log => log.ActionType == ActivityActionType.Deleted && log.UserId == 11), default), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task MoveAsync_DelegatesToWorkflowService()
    {
        var workflowService = new Mock<IWorkflowService>();
        workflowService.Setup(x => x.ExecuteTransitionAsync(7, DoneStatusId, 11, 4m, default)).ReturnsAsync(new WorkflowTransitionResult(true, null, null, 4m));
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(new Issue
        {
            Id = 7,
            ProjectId = 1,
            IssueKey = "PROJ-7",
            Title = "Moved issue",
            WorkflowStatus = new WorkflowStatus { Id = DoneStatusId, Name = "Done", Category = StatusCategory.Done, WorkflowDefinitionId = 1 },
            Assignees = new List<IssueAssignee>()
        });
        var service = CreateService(issueRepository: issueRepository, workflowService: workflowService);

        var moved = await service.MoveAsync(7, DoneStatusId, 4m, 11);

        Assert.True(moved);
        workflowService.Verify(x => x.ExecuteTransitionAsync(7, DoneStatusId, 11, 4m, default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_SubtaskWithoutParent_ThrowsValidationException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(CreateModel(type: IssueType.Subtask)));
    }

    [Fact]
    public async Task CreateAsync_SubtaskWithValidParent_SetsParentIssueId()
    {
        var parentStatus = CreateStatus(BacklogStatusId, "Backlog", StatusCategory.ToDo);
        var parent = CreateIssue(10, parentStatus, IssueType.Task);
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(10, default)).ReturnsAsync(parent);
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, BacklogStatusId, default)).ReturnsAsync(1m);
        issueRepository.Setup(x => x.GetNextIssueSequenceAsync(1, default)).ReturnsAsync(1);
        issueRepository.Setup(x => x.AddAsync(It.IsAny<Issue>(), default)).Returns(Task.CompletedTask);
        var service = CreateService(issueRepository: issueRepository);

        var result = await service.CreateAsync(CreateModel(type: IssueType.Subtask, parentIssueId: 10));

        Assert.Equal(10, result.ParentIssueId);
    }

    [Fact]
    public async Task CreateAsync_StoryWithEpicParent_SetsParentIssueId()
    {
        var epicStatus = CreateStatus(BacklogStatusId, "Backlog", StatusCategory.ToDo);
        var epic = CreateIssue(20, epicStatus, IssueType.Epic);
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(20, default)).ReturnsAsync(epic);
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, BacklogStatusId, default)).ReturnsAsync(1m);
        issueRepository.Setup(x => x.GetNextIssueSequenceAsync(1, default)).ReturnsAsync(1);
        issueRepository.Setup(x => x.AddAsync(It.IsAny<Issue>(), default)).Returns(Task.CompletedTask);
        var service = CreateService(issueRepository: issueRepository);

        var result = await service.CreateAsync(CreateModel(type: IssueType.Story, parentIssueId: 20));

        Assert.Equal(20, result.ParentIssueId);
    }

    [Fact]
    public async Task CreateAsync_StoryWithNonEpicParent_ThrowsValidationException()
    {
        var parentStatus = CreateStatus(BacklogStatusId, "Backlog", StatusCategory.ToDo);
        var parent = CreateIssue(10, parentStatus, IssueType.Task);
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(10, default)).ReturnsAsync(parent);
        var service = CreateService(issueRepository: issueRepository);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(CreateModel(type: IssueType.Story, parentIssueId: 10)));
    }

    [Fact]
    public async Task CreateAsync_SubtaskWithEpicParent_ThrowsValidationException()
    {
        var epicStatus = CreateStatus(BacklogStatusId, "Backlog", StatusCategory.ToDo);
        var epic = CreateIssue(15, epicStatus, IssueType.Epic);
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(15, default)).ReturnsAsync(epic);
        var service = CreateService(issueRepository: issueRepository);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(CreateModel(type: IssueType.Subtask, parentIssueId: 15)));
    }

    [Fact]
    public async Task UpdateParentAsync_WhenEpicLinkChanges_SavesIssueAndWritesActivityLog()
    {
        var storyStatus = CreateStatus(BacklogStatusId, "Backlog", StatusCategory.ToDo);
        var currentEpic = CreateIssue(20, storyStatus, IssueType.Epic);
        var nextEpic = CreateIssue(21, storyStatus, IssueType.Epic);
        var story = CreateIssue(7, storyStatus, IssueType.Story);
        story.ParentIssueId = currentEpic.Id;
        story.ParentIssue = currentEpic;

        var issueRepository = new Mock<IIssueRepository>();
        var activityLogRepository = new Mock<IActivityLogRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(story);
        issueRepository.Setup(x => x.GetByIdAsync(21, default)).ReturnsAsync(nextEpic);
        var service = CreateService(issueRepository: issueRepository, activityLogRepository: activityLogRepository, unitOfWork: unitOfWork);

        var updated = await service.UpdateParentAsync(7, 21, 11);

        Assert.NotNull(updated);
        Assert.Equal(21, story.ParentIssueId);
        activityLogRepository.Verify(
            x => x.AddAsync(It.Is<ActivityLog>(log => log.FieldName == "Epic link" && log.OldValue == "PROJ-20" && log.NewValue == "PROJ-21" && log.UserId == 11), default),
            Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }
    [Fact]
    public async Task UpdateAsync_IssueNotFound_ThrowsNotFoundException()
    {
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync((Issue?)null);
        var service = CreateService(issueRepository: issueRepository);

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(CreateModel(id: 7)));
    }

    private static IssueService CreateService(
        Mock<IIssueRepository>? issueRepository = null,
        Mock<IUserRepository>? userRepository = null,
        Mock<IProjectRepository>? projectRepository = null,
        Mock<ICommentRepository>? commentRepository = null,
        Mock<IAttachmentRepository>? attachmentRepository = null,
        Mock<IAuthorizationService>? authorization = null,
        Mock<IActivityLogRepository>? activityLogRepository = null,
        Mock<IWorkflowService>? workflowService = null,
        Mock<IWorkflowRepository>? workflowRepository = null,
        Mock<IWatcherRepository>? watcherRepository = null,
        Mock<INotificationRepository>? notificationRepository = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        projectRepository ??= new Mock<IProjectRepository>();
        projectRepository.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(new Project { Id = 1, Key = "PROJ", Name = "Project" });

        workflowRepository ??= new Mock<IWorkflowRepository>();
        var defaultWorkflow = CreateWorkflowDefinition();
        workflowRepository.Setup(x => x.GetDefaultByProjectAsync(1, default)).ReturnsAsync(defaultWorkflow);
        workflowRepository.Setup(x => x.GetStatusByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((int statusId, CancellationToken _) => defaultWorkflow.Statuses.FirstOrDefault(status => status.Id == statusId));

        if (issueRepository is null)
        {
            issueRepository = new Mock<IIssueRepository>();
            issueRepository.Setup(x => x.GetNextIssueSequenceAsync(1, default)).ReturnsAsync(1);
            issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, It.IsAny<int>(), default)).ReturnsAsync(1m);
        }

        watcherRepository ??= new Mock<IWatcherRepository>();
        watcherRepository.Setup(x => x.GetByIssueIdAsync(It.IsAny<int>(), default)).ReturnsAsync(Array.Empty<Watcher>());

        workflowService ??= new Mock<IWorkflowService>();
        workflowService.Setup(x => x.ExecuteTransitionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal?>(), default)).ReturnsAsync(new WorkflowTransitionResult(true, null, null, 1m));

        return new IssueService(
            issueRepository.Object,
            (userRepository ?? new Mock<IUserRepository>()).Object,
            projectRepository.Object,
            (commentRepository ?? new Mock<ICommentRepository>()).Object,
            (attachmentRepository ?? new Mock<IAttachmentRepository>()).Object,
            (authorization ?? new Mock<IAuthorizationService>()).Object,
            (activityLogRepository ?? new Mock<IActivityLogRepository>()).Object,
            workflowService.Object,
            workflowRepository.Object,
            watcherRepository.Object,
            (notificationRepository ?? new Mock<INotificationRepository>()).Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }

    private static IssueEditModel CreateModel(
        int? id = null,
        string title = "Issue title",
        int workflowStatusId = BacklogStatusId,
        IssueType type = IssueType.Task,
        int? parentIssueId = null,
        string? descriptionText = null,
        DateOnly? dueDate = null)
    {
        return new IssueEditModel
        {
            Id = id,
            ProjectId = 1,
            Title = title,
            DescriptionText = descriptionText,
            Type = type,
            WorkflowStatusId = workflowStatusId,
            Priority = IssuePriority.Medium,
            ReporterId = 3,
            CreatedById = 9,
            DueDate = dueDate,
            ParentIssueId = parentIssueId,
            AssigneeIds = Array.Empty<int>()
        };
    }

    private static WorkflowDefinition CreateWorkflowDefinition()
    {
        var workflow = new WorkflowDefinition { Id = 1, ProjectId = 1, Name = "Default", IsDefault = true };
        workflow.Statuses.Add(CreateStatus(BacklogStatusId, "Backlog", StatusCategory.ToDo, workflow));
        workflow.Statuses.Add(CreateStatus(SelectedStatusId, "Selected", StatusCategory.ToDo, workflow));
        workflow.Statuses.Add(CreateStatus(DoneStatusId, "Done", StatusCategory.Done, workflow));
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

    private static Issue CreateIssue(int id, WorkflowStatus status, IssueType type = IssueType.Task)
    {
        var issue = new Issue { Id = id, ProjectId = 1, IssueKey = $"PROJ-{id}", Title = "Issue", ReporterId = 3, CreatedById = 9, Type = type };
        issue.WorkflowStatus = status;
        issue.MoveTo(status.Id, 1m);
        return issue;
    }
}







