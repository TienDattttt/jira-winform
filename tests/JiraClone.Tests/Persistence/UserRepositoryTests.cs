using JiraClone.Domain.Entities;
using JiraClone.Persistence;
using JiraClone.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Tests.Persistence;

public class UserRepositoryTests
{
    [Fact]
    public async Task GetByUserNameAsync_ExistingUser_ReturnsUserWithRoles()
    {
        // Arrange
        await using var db = CreateContext();
        SeedUserGraph(db);
        var repository = new UserRepository(db);

        // Act
        var user = await repository.GetByUserNameAsync("admin");

        // Assert
        Assert.NotNull(user);
        Assert.Single(user!.UserRoles);
        Assert.Equal("Admin", user.UserRoles.First().Role.Name);
    }

    [Fact]
    public async Task GetByUserNameAsync_UsernameNotFound_ReturnsNull()
    {
        // Arrange
        await using var db = CreateContext();
        var repository = new UserRepository(db);

        // Act
        var user = await repository.GetByUserNameAsync("missing");

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task GetProjectUsersAsync_ProjectHasMembers_ReturnsUsers()
    {
        // Arrange
        await using var db = CreateContext();
        SeedUserGraph(db);
        var repository = new UserRepository(db);

        // Act
        var users = await repository.GetProjectUsersAsync(1);

        // Assert
        Assert.Single(users);
        Assert.Equal("admin", users[0].UserName);
    }

    [Fact]
    public async Task GetProjectUsersAsync_IncludeChain_LoadsRolesWithoutException()
    {
        // Arrange
        await using var db = CreateContext();
        SeedUserGraph(db);
        var repository = new UserRepository(db);

        // Act
        var users = await repository.GetProjectUsersAsync(1);

        // Assert
        Assert.Equal("Admin", users[0].UserRoles.Single().Role.Name);
    }

    private static JiraCloneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<JiraCloneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new JiraCloneDbContext(options);
    }

    private static void SeedUserGraph(JiraCloneDbContext db)
    {
        var role = new Role { Id = 1, Name = "Admin", Description = "Admin" };
        var user = new User { Id = 1, UserName = "admin", DisplayName = "Admin User", Email = "admin@example.com", PasswordHash = "h", PasswordSalt = "s" };
        var project = new Project { Id = 1, Key = "PROJ", Name = "Project" };
        db.Roles.Add(role);
        db.Users.Add(user);
        db.Projects.Add(project);
        db.UserRoles.Add(new UserRole { UserId = 1, User = user, RoleId = 1, Role = role });
        db.ProjectMembers.Add(new ProjectMember { ProjectId = 1, Project = project, UserId = 1, User = user });
        db.SaveChanges();
    }
}
