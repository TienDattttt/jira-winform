using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.Persistence;
using JiraClone.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Tests.Persistence;

public class IssueRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsIssueWithReporter()
    {
        // Arrange
        await using var db = CreateContext();
        var reporter = new User { Id = 1, UserName = "admin", DisplayName = "Admin", Email = "a@a.com", PasswordHash = "h", PasswordSalt = "s" };
        db.Users.Add(reporter);
        db.Issues.Add(new Issue { Id = 10, ProjectId = 1, IssueKey = "JIRA-1", Title = "Issue", ReporterId = 1, Reporter = reporter, CreatedById = 1 });
        await db.SaveChangesAsync();
        var repository = new IssueRepository(db);

        // Act
        var issue = await repository.GetByIdAsync(10);

        // Assert
        Assert.NotNull(issue);
        Assert.NotNull(issue!.Reporter);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        // Arrange
        await using var db = CreateContext();
        var repository = new IssueRepository(db);

        // Act
        var issue = await repository.GetByIdAsync(999);

        // Assert
        Assert.Null(issue);
    }

    [Fact]
    public async Task GetProjectIssuesAsync_ReturnsOnlyProjectIssues()
    {
        // Arrange
        await using var db = CreateContext();
        var reporter = new User { Id = 1, UserName = "admin", DisplayName = "Admin", Email = "a@a.com", PasswordHash = "h", PasswordSalt = "s" };
        db.Users.Add(reporter);
        db.Issues.AddRange(
            new Issue { ProjectId = 1, IssueKey = "JIRA-1", Title = "P1", ReporterId = 1, Reporter = reporter, CreatedById = 1 },
            new Issue { ProjectId = 2, IssueKey = "JIRA-2", Title = "P2", ReporterId = 1, Reporter = reporter, CreatedById = 1 });
        await db.SaveChangesAsync();
        var repository = new IssueRepository(db);

        // Act
        var issues = await repository.GetProjectIssuesAsync(1);

        // Assert
        Assert.Single(issues);
        Assert.Equal(1, issues[0].ProjectId);
    }

    [Fact]
    public async Task GetProjectIssuesAsync_SoftDeletedIssue_DoesNotAppear()
    {
        // Arrange
        await using var db = CreateContext();
        var reporter = new User { Id = 1, UserName = "admin", DisplayName = "Admin", Email = "a@a.com", PasswordHash = "h", PasswordSalt = "s" };
        db.Users.Add(reporter);
        db.Issues.Add(new Issue { ProjectId = 1, IssueKey = "JIRA-1", Title = "Hidden", ReporterId = 1, Reporter = reporter, CreatedById = 1, IsDeleted = true });
        await db.SaveChangesAsync();
        var repository = new IssueRepository(db);

        // Act
        var issues = await repository.GetProjectIssuesAsync(1);

        // Assert
        Assert.Empty(issues);
    }

    [Fact]
    public async Task AddAsync_ThenSave_IssueAppearsInSubsequentQuery()
    {
        // Arrange
        await using var db = CreateContext();
        var reporter = new User { Id = 1, UserName = "admin", DisplayName = "Admin", Email = "a@a.com", PasswordHash = "h", PasswordSalt = "s" };
        db.Users.Add(reporter);
        await db.SaveChangesAsync();
        var repository = new IssueRepository(db);
        var issue = new Issue { ProjectId = 1, IssueKey = "JIRA-1", Title = "Created", ReporterId = 1, CreatedById = 1 };

        // Act
        await repository.AddAsync(issue);
        await db.SaveChangesAsync();
        var loaded = await repository.GetProjectIssuesAsync(1);

        // Assert
        Assert.Single(loaded);
        Assert.Equal("Created", loaded[0].Title);
    }

    private static JiraCloneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<JiraCloneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new JiraCloneDbContext(options);
    }
}
