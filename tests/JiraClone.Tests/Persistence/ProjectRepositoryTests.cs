using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.Persistence;
using JiraClone.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Tests.Persistence;

public class ProjectRepositoryTests
{
    [Fact]
    public async Task GetActiveProjectAsync_ProjectGraphExists_ReturnsBoardColumnsMembersAndRoles()
    {
        // Arrange
        await using var db = CreateContext();
        SeedProjectGraph(db);
        var repository = new ProjectRepository(db);

        // Act
        var project = await repository.GetActiveProjectAsync();

        // Assert
        Assert.NotNull(project);
        Assert.Single(project!.BoardColumns);
        Assert.Single(project.Members);
        Assert.Equal("Admin", project.Members.Single().User.UserRoles.Single().Role.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ProjectExists_ReturnsProjectWithMembersAndColumns()
    {
        // Arrange
        await using var db = CreateContext();
        SeedProjectGraph(db);
        var repository = new ProjectRepository(db);

        // Act
        var project = await repository.GetByIdAsync(1);

        // Assert
        Assert.NotNull(project);
        Assert.Equal("PROJ", project!.Key);
        Assert.Equal(IssueStatus.Backlog, project.BoardColumns.Single().StatusCode);
        Assert.Equal("admin", project.Members.Single().User.UserName);
    }

    private static JiraCloneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<JiraCloneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new JiraCloneDbContext(options);
    }

    private static void SeedProjectGraph(JiraCloneDbContext db)
    {
        var role = new Role { Id = 1, Name = "Admin", Description = "Admin" };
        var user = new User { Id = 1, UserName = "admin", DisplayName = "Admin User", Email = "admin@example.com", PasswordHash = "h", PasswordSalt = "s" };
        var project = new Project { Id = 1, Key = "PROJ", Name = "Project", IsActive = true };
        var column = new BoardColumn { Id = 1, ProjectId = 1, Project = project, Name = "Backlog", StatusCode = IssueStatus.Backlog, DisplayOrder = 1 };
        var member = new ProjectMember { ProjectId = 1, Project = project, UserId = 1, User = user, ProjectRole = ProjectRole.ProjectManager };
        var userRole = new UserRole { UserId = 1, User = user, RoleId = 1, Role = role };

        db.Roles.Add(role);
        db.Users.Add(user);
        db.Projects.Add(project);
        db.BoardColumns.Add(column);
        db.ProjectMembers.Add(member);
        db.UserRoles.Add(userRole);
        db.SaveChanges();
    }
}
