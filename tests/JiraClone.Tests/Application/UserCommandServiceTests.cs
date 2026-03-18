using System.ComponentModel.DataAnnotations;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Users;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Moq;

namespace JiraClone.Tests.Application;

public class UserCommandServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidInput_WritesActivityLog()
    {
        // Arrange
        var users = new Mock<IUserRepository>();
        var projects = new Mock<IProjectRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var activityLogs = new Mock<IActivityLogRepository>();
        users.Setup(x => x.GetRolesAsync(default)).ReturnsAsync([new Role { Id = 1, Name = "Developer" }]);
        users.Setup(x => x.AddAsync(It.IsAny<User>(), default)).Callback<User, CancellationToken>((user, _) => user.Id = 42).Returns(Task.CompletedTask);
        projects.Setup(x => x.GetByIdAsync(1, default)).ReturnsAsync(new Project { Id = 1, Key = "PROJ", Name = "Project" });
        hasher.Setup(x => x.Hash("Secret123")).Returns(("hash", "salt"));
        var service = CreateService(users, projects, hasher, activityLogs: activityLogs);

        // Act
        var user = await service.CreateAsync(1, "dev1", "Dev One", "dev1@example.com", "Secret123", ProjectRole.Developer, ["Developer"]);

        // Assert
        Assert.Equal(42, user.Id);
        activityLogs.Verify(x => x.AddAsync(It.Is<ActivityLog>(log => log.ProjectId == 1 && log.ActionType == ActivityActionType.Created && log.NewValue == "dev1"), default), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithProjectRole_UpdatesMembershipsAndSavesChanges()
    {
        // Arrange
        var user = new User
        {
            Id = 7,
            UserName = "dev1",
            DisplayName = "Dev One",
            Email = "dev1@example.com",
            ProjectMemberships = [new ProjectMember { ProjectId = 1, UserId = 7, ProjectRole = ProjectRole.Developer }]
        };
        var users = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        users.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(user);
        users.Setup(x => x.GetRolesAsync(default)).ReturnsAsync([new Role { Id = 1, Name = "Admin" }]);
        var service = CreateService(users: users, unitOfWork: unitOfWork);

        // Act
        var updated = await service.UpdateAsync(7, "Developer One", "new@example.com", true, ProjectRole.ProjectManager, ["Admin"]);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(ProjectRole.ProjectManager, user.ProjectMemberships.Single().ProjectRole);
        Assert.Single(user.UserRoles);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_ExistingUser_HashesPasswordAndSavesChanges()
    {
        // Arrange
        var user = new User { Id = 7, UserName = "dev1", DisplayName = "Dev One", Email = "dev1@example.com", ProjectMemberships = [new ProjectMember { ProjectId = 1, UserId = 7 }] };
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var unitOfWork = new Mock<IUnitOfWork>();
        users.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(user);
        hasher.Setup(x => x.Hash("NewSecret123")).Returns(("hashed", "salted"));
        var service = CreateService(users: users, passwordHasher: hasher, unitOfWork: unitOfWork);

        // Act
        var reset = await service.ResetPasswordAsync(7, "NewSecret123");

        // Assert
        Assert.True(reset);
        Assert.Equal("hashed", user.PasswordHash);
        Assert.Equal("salted", user.PasswordSalt);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_UserNotFound_ReturnsFalse()
    {
        // Arrange
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync((User?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = CreateService(users: users, unitOfWork: unitOfWork);

        // Act
        var reset = await service.ResetPasswordAsync(7, "NewSecret123");

        // Assert
        Assert.False(reset);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WeakPassword_ThrowsValidationException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(1, "dev1", "Dev One", "dev1@example.com", "weakpass", ProjectRole.Developer, ["Developer"]));
    }

    [Fact]
    public async Task ResetPasswordAsync_WeakPassword_ThrowsValidationException()
    {
        var user = new User { Id = 7, UserName = "dev1", DisplayName = "Dev One", Email = "dev1@example.com" };
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(7, default)).ReturnsAsync(user);
        var service = CreateService(users: users);

        await Assert.ThrowsAsync<ValidationException>(() => service.ResetPasswordAsync(7, "weakpass"));
    }
    private static UserCommandService CreateService(
        Mock<IUserRepository>? users = null,
        Mock<IProjectRepository>? projects = null,
        Mock<IPasswordHasher>? passwordHasher = null,
        Mock<IAuthorizationService>? authorization = null,
        Mock<IActivityLogRepository>? activityLogs = null,
        Mock<ICurrentUserContext>? currentUserContext = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        currentUserContext ??= new Mock<ICurrentUserContext>();
        currentUserContext.Setup(x => x.CurrentUser).Returns(new User { Id = 99, UserName = "admin", DisplayName = "Admin User", Email = "admin@example.com" });

        return new UserCommandService(
            (users ?? new Mock<IUserRepository>()).Object,
            (projects ?? new Mock<IProjectRepository>()).Object,
            (passwordHasher ?? new Mock<IPasswordHasher>()).Object,
            (authorization ?? new Mock<IAuthorizationService>()).Object,
            (activityLogs ?? new Mock<IActivityLogRepository>()).Object,
            currentUserContext.Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }
}


