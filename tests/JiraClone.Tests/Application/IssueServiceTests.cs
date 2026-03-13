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
        // Arrange
        var issueRepository = new Mock<IIssueRepository>();
        var userRepository = new Mock<IUserRepository>();
        var commentRepository = new Mock<ICommentRepository>();
        var attachmentRepository = new Mock<IAttachmentRepository>();
        var authorization = new Mock<IAuthorizationService>();
        var activityLogRepository = new Mock<IActivityLogRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        issueRepository.Setup(x => x.GetNextBoardPositionAsync(1, IssueStatus.Selected, default)).ReturnsAsync(1m);
        issueRepository.Setup(x => x.AddAsync(It.IsAny<Issue>(), default)).Callback<Issue, CancellationToken>((issue, _) => issue.Id = 42).Returns(Task.CompletedTask);
        var service = CreateService(issueRepository, userRepository, commentRepository, attachmentRepository, authorization, activityLogRepository, unitOfWork);
        var model = CreateModel(status: IssueStatus.Selected);

        // Act
        var result = await service.CreateAsync(model);

        // Assert
        Assert.Equal(42, result.Id);
        issueRepository.Verify(x => x.AddAsync(It.IsAny<Issue>(), default), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_MissingTitle_ThrowsValidationException()
    {
        // Arrange
        var service = CreateService();
        var model = CreateModel(title: " ");

        // Act
        var act = () => service.CreateAsync(model);

        // Assert
        await Assert.ThrowsAsync<ValidationException>(act);
    }

    [Fact]
    public async Task UpdateAsync_IssueNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync((Issue?)null);
        var service = CreateService(issueRepository: issueRepository);
        var model = CreateModel(id: 7);

        // Act
        var act = () => service.UpdateAsync(model);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task UpdateAsync_ValidInput_CallsSaveChanges()
    {
        // Arrange
        var issue = new Issue { Id = 7, ProjectId = 1, Title = "Old", ReporterId = 3, CreatedById = 9 };
        var issueRepository = new Mock<IIssueRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(issue);
        var service = CreateService(issueRepository: issueRepository, unitOfWork: unitOfWork);
        var model = CreateModel(id: 7, title: "Updated");

        // Act
        await service.UpdateAsync(model);

        // Assert
        Assert.Equal("Updated", issue.Title);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_IssueExists_SoftDeletesAndWritesActivityLog()
    {
        // Arrange
        var issue = new Issue { Id = 7, ProjectId = 1, Title = "Delete me", ReporterId = 3, CreatedById = 9 };
        var issueRepository = new Mock<IIssueRepository>();
        var activityLogRepository = new Mock<IActivityLogRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(issue);
        issueRepository.Setup(x => x.RemoveAsync(issue, default)).Callback<Issue, CancellationToken>((entity, _) => entity.IsDeleted = true).Returns(Task.CompletedTask);
        var service = CreateService(issueRepository: issueRepository, activityLogRepository: activityLogRepository, unitOfWork: unitOfWork);

        // Act
        var deleted = await service.DeleteAsync(7, 11);

        // Assert
        Assert.True(deleted);
        Assert.True(issue.IsDeleted);
        activityLogRepository.Verify(x => x.AddAsync(It.Is<ActivityLog>(log => log.ActionType == ActivityActionType.Deleted && log.UserId == 11), default), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task MoveAsync_StatusChanged_SavesNewStatus()
    {
        // Arrange
        var issue = new Issue { Id = 7, ProjectId = 1, Title = "Move me", ReporterId = 3, CreatedById = 9 };
        var unitOfWork = new Mock<IUnitOfWork>();
        var issueRepository = new Mock<IIssueRepository>();
        issueRepository.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(issue);
        var service = CreateService(issueRepository: issueRepository, unitOfWork: unitOfWork);

        // Act
        var moved = await service.MoveAsync(7, IssueStatus.Done, 4m, 11);

        // Assert
        Assert.True(moved);
        Assert.Equal(IssueStatus.Done, issue.Status);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    private static IssueService CreateService(
        Mock<IIssueRepository>? issueRepository = null,
        Mock<IUserRepository>? userRepository = null,
        Mock<ICommentRepository>? commentRepository = null,
        Mock<IAttachmentRepository>? attachmentRepository = null,
        Mock<IAuthorizationService>? authorization = null,
        Mock<IActivityLogRepository>? activityLogRepository = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        return new IssueService(
            (issueRepository ?? new Mock<IIssueRepository>()).Object,
            (userRepository ?? new Mock<IUserRepository>()).Object,
            (commentRepository ?? new Mock<ICommentRepository>()).Object,
            (attachmentRepository ?? new Mock<IAttachmentRepository>()).Object,
            (authorization ?? new Mock<IAuthorizationService>()).Object,
            (activityLogRepository ?? new Mock<IActivityLogRepository>()).Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }

    private static IssueEditModel CreateModel(int? id = null, string title = "Issue title", IssueStatus status = IssueStatus.Backlog)
    {
        return new IssueEditModel
        {
            Id = id,
            ProjectId = 1,
            Title = title,
            Type = IssueType.Task,
            Status = status,
            Priority = IssuePriority.Medium,
            ReporterId = 3,
            CreatedById = 9,
            AssigneeIds = Array.Empty<int>()
        };
    }
}
