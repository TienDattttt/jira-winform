using JiraClone.Domain.Entities;
using JiraClone.Persistence;
using JiraClone.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Tests.Persistence;

public class CommentRepositoryTests
{
    [Fact]
    public async Task GetByIssueIdAsync_ActiveComments_ReturnsUserGraphInCreatedOrder()
    {
        // Arrange
        await using var db = CreateContext();
        SeedUsers(db);
        db.Comments.AddRange(
            new Comment { Id = 1, IssueId = 10, UserId = 1, Body = "First", CreatedAtUtc = new DateTime(2026, 3, 1) },
            new Comment { Id = 2, IssueId = 10, UserId = 2, Body = "Second", CreatedAtUtc = new DateTime(2026, 3, 2) });
        await db.SaveChangesAsync();
        var repository = new CommentRepository(db);

        // Act
        var comments = await repository.GetByIssueIdAsync(10);

        // Assert
        Assert.Equal(2, comments.Count);
        Assert.Equal("Alice", comments[0].User.DisplayName);
        Assert.Equal("Second", comments[1].Body);
    }

    [Fact]
    public async Task GetByIssueIdAsync_SoftDeletedComment_DoesNotAppear()
    {
        // Arrange
        await using var db = CreateContext();
        SeedUsers(db);
        db.Comments.Add(new Comment { Id = 1, IssueId = 10, UserId = 1, Body = "Hidden", IsDeleted = true });
        await db.SaveChangesAsync();
        var repository = new CommentRepository(db);

        // Act
        var comments = await repository.GetByIssueIdAsync(10);

        // Assert
        Assert.Empty(comments);
    }

    private static JiraCloneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<JiraCloneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new JiraCloneDbContext(options);
    }

    private static void SeedUsers(JiraCloneDbContext db)
    {
        db.Users.AddRange(
            new User { Id = 1, UserName = "alice", DisplayName = "Alice", Email = "alice@example.com", PasswordHash = "h", PasswordSalt = "s" },
            new User { Id = 2, UserName = "bob", DisplayName = "Bob", Email = "bob@example.com", PasswordHash = "h", PasswordSalt = "s" });
        db.SaveChanges();
    }
}
