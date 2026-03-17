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
    [Fact]
    public async Task CreateAsync_ValidInput_SavesIssueAndReturnsEntity()
    {
        var issueRepository = new Mock<IIssueRepository>();
        var userRepository = new Mock<IUserRepository>();
        var projectRepository = new Mock<IProjectRepository>();
        var commentRepository = new Mock<ICommentRepository>();
        var attachmentRepository = new Mock<IAttachmentRepository>();
        var authorization = new Mock<IAuthorizationService>();
        var activityLogRepository = new Mock<IActivityLogRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, IssueStatus.Selected, default)).ReturnsAsync(1m);
        issueRepository.Setup(x => x.GetNextIssueSequenceAsync(1, default)).ReturnsAsync(1);
        issueRepository.Setup(x => x.AddAsync(It.IsAny<Issue>(), default)).Callback<Issue, CancellationToken>((issue, _) => issue.Id = 42).Returns(Task.CompletedTask);
        projectRepository.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(new Project { Id = 1, Key = "PROJ", Name = "Project" });
        var service = CreateService(issueRepository, userRepository, projectRepository, commentRepository, attachmentRepository, authorization, activityLogRepository, unitOfWork);
        var model = CreateModel(status: IssueStatus.Selected);

        var result = await service.CreateAsync(model);

        Assert.Equal(42, result.Id);
        Assert.Equal("PROJ-1", result.IssueKey);
        issueRepository.Verify(x => x.AddAsync(It.IsAny<Issue>(), default), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_RendersDescriptionHtmlFromMarkdown()
    {
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, IssueStatus.Backlog, default)).ReturnsAsync(1m);
        issueRepository.Setup(x => x.GetNextIssueSequenceAsync(1, default)).ReturnsAsync(1);
        issueRepository.Setup(x => x.AddAsync(It.IsAny<Issue>(), default)).Returns(Task.CompletedTask);
        var service = CreateService(issueRepository: issueRepository);
        var model = CreateModel(descriptionText: "**Bold** item");

        var result = await service.CreateAsync(model);

        Assert.Equal("**Bold** item", result.DescriptionText);
        Assert.Contains("<strong>Bold</strong>", result.DescriptionHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_ExistingProjectIssues_UsesNextProjectSequence()
    {
        var issueRepository = new Mock<IIssueRepository>();
        var projectRepository = new Mock<IProjectRepository>();
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, IssueStatus.Backlog, default)).ReturnsAsync(1m);
        issueRepository.Setup(x => x.GetNextIssueSequenceAsync(1, default)).ReturnsAsync(8);
        projectRepository.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(new Project { Id = 1, Key = "PROJ", Name = "Project" });
        var service = CreateService(issueRepository: issueRepository, projectRepository: projectRepository);

        var issue = await service.CreateAsync(CreateModel());

        Assert.Equal("PROJ-8", issue.IssueKey);
    }

    [Fact]
    public async Task CreateAsync_MissingTitle_ThrowsValidationException()
    {
        var service = CreateService();
        var model = CreateModel(title: " ");

        var act = () => service.CreateAsync(model);

        await Assert.ThrowsAsync<ValidationException>(act);
    }

    [Fact]
    public async Task UpdateAsync_IssueNotFound_ThrowsNotFoundException()
    {
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync((Issue?)null);
        var service = CreateService(issueRepository: issueRepository);
        var model = CreateModel(id: 7);

        var act = () => service.UpdateAsync(model);

        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task UpdateAsync_ValidInput_CallsSaveChanges()
    {
        var issue = new Issue { Id = 7, ProjectId = 1, Title = "Old", ReporterId = 3, CreatedById = 9 };
        var issueRepository = new Mock<IIssueRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(issue);
        var service = CreateService(issueRepository: issueRepository, unitOfWork: unitOfWork);
        var model = CreateModel(id: 7, title: "Updated");

        await service.UpdateAsync(model);

        Assert.Equal("Updated", issue.Title);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_RendersDescriptionHtmlFromMarkdown()
    {
        var issue = new Issue { Id = 7, ProjectId = 1, Title = "Old", ReporterId = 3, CreatedById = 9 };
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(issue);
        var service = CreateService(issueRepository: issueRepository);
        var model = CreateModel(id: 7, descriptionText: "`code` sample");

        await service.UpdateAsync(model);

        Assert.Equal("`code` sample", issue.DescriptionText);
        Assert.Contains("<code>code</code>", issue.DescriptionHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_IssueExists_SoftDeletesAndWritesActivityLog()
    {
        var issue = new Issue { Id = 7, ProjectId = 1, Title = "Delete me", ReporterId = 3, CreatedById = 9 };
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
    public async Task MoveAsync_StatusChanged_SavesNewStatus()
    {
        var issue = new Issue { Id = 7, ProjectId = 1, Title = "Move me", ReporterId = 3, CreatedById = 9 };
        var unitOfWork = new Mock<IUnitOfWork>();
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(issue);
        var service = CreateService(issueRepository: issueRepository, unitOfWork: unitOfWork);

        var moved = await service.MoveAsync(7, IssueStatus.Done, 4m, 11);

        Assert.True(moved);
        Assert.Equal(IssueStatus.Done, issue.Status);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_SubtaskWithoutParent_ThrowsValidationException()
    {
        var service = CreateService();
        var model = CreateModel(type: IssueType.Subtask);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(model));
    }

    [Fact]
    public async Task CreateAsync_EpicWithParent_ThrowsValidationException()
    {
        var service = CreateService();
        var model = CreateModel(type: IssueType.Epic, parentIssueId: 10);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(model));
    }

    [Fact]
    public async Task CreateAsync_SubtaskWithValidParent_SetsParentIssueId()
    {
        var issueRepository = new Mock<IIssueRepository>();
        var parent = new Issue { Id = 10, ProjectId = 1, Title = "Parent", Type = IssueType.Task, ReporterId = 3, CreatedById = 9 };
        issueRepository.Setup(x => x.GetByIdAsync(10, default)).ReturnsAsync(parent);
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, IssueStatus.Backlog, default)).ReturnsAsync(1m);
        issueRepository.Setup(x => x.GetNextIssueSequenceAsync(1, default)).ReturnsAsync(1);
        issueRepository.Setup(x => x.AddAsync(It.IsAny<Issue>(), default)).Returns(Task.CompletedTask);
        var service = CreateService(issueRepository: issueRepository);
        var model = CreateModel(type: IssueType.Subtask, parentIssueId: 10);

        var result = await service.CreateAsync(model);

        Assert.Equal(10, result.ParentIssueId);
    }

    [Fact]
    public async Task CreateAsync_SubtaskOfSubtask_ThrowsValidationException()
    {
        var issueRepository = new Mock<IIssueRepository>();
        var parent = new Issue { Id = 10, ProjectId = 1, Title = "Sub parent", Type = IssueType.Subtask, ReporterId = 3, CreatedById = 9 };
        issueRepository.Setup(x => x.GetByIdAsync(10, default)).ReturnsAsync(parent);
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, IssueStatus.Backlog, default)).ReturnsAsync(1m);
        issueRepository.Setup(x => x.GetNextIssueSequenceAsync(1, default)).ReturnsAsync(1);
        var service = CreateService(issueRepository: issueRepository);
        var model = CreateModel(type: IssueType.Subtask, parentIssueId: 10);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(model));
    }

    private static IssueService CreateService(
        Mock<IIssueRepository>? issueRepository = null,
        Mock<IUserRepository>? userRepository = null,
        Mock<IProjectRepository>? projectRepository = null,
        Mock<ICommentRepository>? commentRepository = null,
        Mock<IAttachmentRepository>? attachmentRepository = null,
        Mock<IAuthorizationService>? authorization = null,
        Mock<IActivityLogRepository>? activityLogRepository = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        projectRepository ??= new Mock<IProjectRepository>();
        projectRepository.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(new Project { Id = 1, Key = "PROJ", Name = "Project" });

        if (issueRepository is null)
        {
            issueRepository = new Mock<IIssueRepository>();
            issueRepository.Setup(x => x.GetNextIssueSequenceAsync(1, default)).ReturnsAsync(1);
            issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, It.IsAny<IssueStatus>(), default)).ReturnsAsync(1m);
        }

        return new IssueService(
            issueRepository.Object,
            (userRepository ?? new Mock<IUserRepository>()).Object,
            projectRepository.Object,
            (commentRepository ?? new Mock<ICommentRepository>()).Object,
            (attachmentRepository ?? new Mock<IAttachmentRepository>()).Object,
            (authorization ?? new Mock<IAuthorizationService>()).Object,
            (activityLogRepository ?? new Mock<IActivityLogRepository>()).Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }

    private static IssueEditModel CreateModel(
        int? id = null,
        string title = "Issue title",
        IssueStatus status = IssueStatus.Backlog,
        IssueType type = IssueType.Task,
        int? parentIssueId = null,
        string? descriptionText = null)
    {
        return new IssueEditModel
        {
            Id = id,
            ProjectId = 1,
            Title = title,
            DescriptionText = descriptionText,
            Type = type,
            Status = status,
            Priority = IssuePriority.Medium,
            ReporterId = 3,
            CreatedById = 9,
            ParentIssueId = parentIssueId,
            AssigneeIds = Array.Empty<int>()
        };
    }
}
