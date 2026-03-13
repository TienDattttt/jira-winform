using JiraClone.Application.Abstractions;
using JiraClone.Application.Projects;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Moq;

namespace JiraClone.Tests.Application;

public class ProjectCommandServiceTests
{
    [Fact]
    public async Task UpdateProjectAsync_ValidChange_WritesActivityLog()
    {
        // Arrange
        var project = new Project { Id = 1, Key = "PROJ", Name = "Old Name" };
        var projects = new Mock<IProjectRepository>();
        var activityLogs = new Mock<IActivityLogRepository>();
        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(project);
        var service = CreateService(projects: projects, activityLogs: activityLogs);

        // Act
        await service.UpdateProjectAsync(1, "New Name", null, ProjectCategory.Software, null);

        // Assert
        activityLogs.Verify(x => x.AddAsync(It.Is<ActivityLog>(log => log.ProjectId == 1 && log.FieldName == nameof(Project.Name) && log.NewValue == "New Name"), default), Times.Once);
    }

    [Fact]
    public async Task AddMemberAsync_NewMember_AddsMembershipAndSavesChanges()
    {
        // Arrange
        var project = new Project { Id = 1, Key = "PROJ", Name = "Project" };
        var user = new User { Id = 5, UserName = "dev1", DisplayName = "Dev One", Email = "dev1@example.com" };
        var projects = new Mock<IProjectRepository>();
        var users = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(project);
        users.Setup(x => x.GetByIdAsync(5, default)).ReturnsAsync(user);
        var service = CreateService(projects: projects, users: users, unitOfWork: unitOfWork);

        // Act
        var added = await service.AddMemberAsync(1, 5, ProjectRole.Developer);

        // Assert
        Assert.True(added);
        Assert.Single(project.Members);
        Assert.Equal(5, project.Members.Single().UserId);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task AddMemberAsync_DuplicateMembership_ReturnsFalseWithoutSaving()
    {
        // Arrange
        var project = new Project { Id = 1, Members = [new ProjectMember { ProjectId = 1, UserId = 5, ProjectRole = ProjectRole.Developer }] };
        var projects = new Mock<IProjectRepository>();
        var users = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(project);
        users.Setup(x => x.GetByIdAsync(5, default)).ReturnsAsync(new User { Id = 5, UserName = "dev1", DisplayName = "Dev One", Email = "dev1@example.com" });
        var service = CreateService(projects: projects, users: users, unitOfWork: unitOfWork);

        // Act
        var added = await service.AddMemberAsync(1, 5, ProjectRole.Developer);

        // Assert
        Assert.False(added);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task UpdateBoardColumnAsync_ValidInput_UpdatesColumnAndSavesChanges()
    {
        // Arrange
        var column = new BoardColumn { Id = 7, ProjectId = 1, Name = "Doing", WipLimit = 2 };
        var project = new Project { Id = 1, BoardColumns = [column] };
        var projects = new Mock<IProjectRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(project);
        var service = CreateService(projects: projects, unitOfWork: unitOfWork);

        // Act
        var updated = await service.UpdateBoardColumnAsync(1, 7, "In Progress", 4);

        // Assert
        Assert.True(updated);
        Assert.Equal("In Progress", column.Name);
        Assert.Equal(4, column.WipLimit);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    private static ProjectCommandService CreateService(
        Mock<IProjectRepository>? projects = null,
        Mock<IUserRepository>? users = null,
        Mock<IAuthorizationService>? authorization = null,
        Mock<IActivityLogRepository>? activityLogs = null,
        Mock<ICurrentUserContext>? currentUserContext = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        currentUserContext ??= new Mock<ICurrentUserContext>();
        currentUserContext.Setup(x => x.CurrentUser).Returns(new User { Id = 99, UserName = "admin", DisplayName = "Admin User", Email = "admin@example.com" });

        return new ProjectCommandService(
            (projects ?? new Mock<IProjectRepository>()).Object,
            (users ?? new Mock<IUserRepository>()).Object,
            (authorization ?? new Mock<IAuthorizationService>()).Object,
            (activityLogs ?? new Mock<IActivityLogRepository>()).Object,
            currentUserContext.Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }
}
