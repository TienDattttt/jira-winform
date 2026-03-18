using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Application.Notifications;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Moq;

namespace JiraClone.Tests.Application;

public class NotificationServiceTests
{
    [Fact]
    public async Task CreateNotificationAsync_EmailEnabledRecipient_SendsEmail()
    {
        var notifications = new Mock<INotificationRepository>();
        notifications
            .Setup(x => x.AddAsync(It.IsAny<Notification>(), default))
            .Callback<Notification, CancellationToken>((notification, _) => notification.Id = 42)
            .Returns(Task.CompletedTask);

        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(new User
        {
            Id = 7,
            UserName = "dev1",
            DisplayName = "Dev One",
            Email = "dev1@example.com",
            IsActive = true,
            EmailNotificationsEnabled = true,
        });

        var issues = new Mock<IIssueRepository>();
        issues.Setup(x => x.GetByIdAsync(5, default)).ReturnsAsync(new Issue
        {
            Id = 5,
            ProjectId = 1,
            IssueKey = "PROJ-5",
            Title = "Fix email delivery"
        });

        var projects = new Mock<IProjectRepository>();
        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(new Project
        {
            Id = 1,
            Key = "PROJ",
            Name = "Project"
        });

        var templateRenderer = new Mock<INotificationEmailTemplateRenderer>();
        templateRenderer
            .Setup(x => x.Render(It.IsAny<NotificationEmailTemplateModel>()))
            .Returns("<p>Hello from Jira Desktop</p>");

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(x => x.SendAsync("dev1@example.com", "Dev One", "Assigned to PROJ-5", "<p>Hello from Jira Desktop</p>", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        var service = new NotificationService(
            notifications.Object,
            users.Object,
            issues.Object,
            projects.Object,
            templateRenderer.Object,
            emailService.Object,
            unitOfWork.Object);

        var created = await service.CreateNotificationAsync(
            7,
            NotificationType.IssueAssigned,
            "Assigned to PROJ-5",
            "Someone assigned you to PROJ-5 - Fix email delivery.",
            issueId: 5,
            projectId: 1);

        Assert.Equal(42, created.Id);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
        emailService.Verify(
            x => x.SendAsync("dev1@example.com", "Dev One", "Assigned to PROJ-5", "<p>Hello from Jira Desktop</p>", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateNotificationAsync_EmailDisabledRecipient_DoesNotSendEmail()
    {
        var notifications = new Mock<INotificationRepository>();
        notifications
            .Setup(x => x.AddAsync(It.IsAny<Notification>(), default))
            .Callback<Notification, CancellationToken>((notification, _) => notification.Id = 7)
            .Returns(Task.CompletedTask);

        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(5, default)).ReturnsAsync(new User
        {
            Id = 5,
            UserName = "viewer",
            DisplayName = "Viewer",
            Email = "viewer@example.com",
            IsActive = true,
            EmailNotificationsEnabled = false,
        });

        var emailService = new Mock<IEmailService>();
        var service = new NotificationService(
            notifications.Object,
            users.Object,
            new Mock<IIssueRepository>().Object,
            new Mock<IProjectRepository>().Object,
            new Mock<INotificationEmailTemplateRenderer>().Object,
            emailService.Object,
            new Mock<IUnitOfWork>().Object);

        await service.CreateNotificationAsync(5, NotificationType.CommentAdded, "New comment", "A comment was added.");

        emailService.Verify(
            x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}