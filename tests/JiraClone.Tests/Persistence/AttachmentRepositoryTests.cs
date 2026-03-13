using JiraClone.Domain.Entities;
using JiraClone.Persistence;
using JiraClone.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Tests.Persistence;

public class AttachmentRepositoryTests
{
    [Fact]
    public async Task GetByIssueIdAsync_ActiveAttachments_ReturnsUploaderGraph()
    {
        // Arrange
        await using var db = CreateContext();
        SeedUser(db);
        db.Attachments.AddRange(
            new Attachment { Id = 1, IssueId = 10, UploadedById = 1, OriginalFileName = "a.txt", StoredFileName = "a.bin", ContentType = "text/plain", FileExtension = ".txt", FileSizeBytes = 12, StoragePath = "c:\\files\\a.bin", ChecksumSha256 = "1", UploadedAtUtc = new DateTime(2026, 3, 2) },
            new Attachment { Id = 2, IssueId = 10, UploadedById = 1, OriginalFileName = "b.txt", StoredFileName = "b.bin", ContentType = "text/plain", FileExtension = ".txt", FileSizeBytes = 20, StoragePath = "c:\\files\\b.bin", ChecksumSha256 = "2", UploadedAtUtc = new DateTime(2026, 3, 3) });
        await db.SaveChangesAsync();
        var repository = new AttachmentRepository(db);

        // Act
        var attachments = await repository.GetByIssueIdAsync(10);

        // Assert
        Assert.Equal(2, attachments.Count);
        Assert.Equal("Bob", attachments[0].UploadedBy.DisplayName);
        Assert.Equal("b.txt", attachments[0].OriginalFileName);
    }

    [Fact]
    public async Task GetByIssueIdAsync_SoftDeletedAttachment_DoesNotAppear()
    {
        // Arrange
        await using var db = CreateContext();
        SeedUser(db);
        db.Attachments.Add(new Attachment { Id = 1, IssueId = 10, UploadedById = 1, OriginalFileName = "hidden.txt", StoredFileName = "hidden.bin", ContentType = "text/plain", FileExtension = ".txt", FileSizeBytes = 12, StoragePath = "c:\\files\\hidden.bin", ChecksumSha256 = "1", IsDeleted = true });
        await db.SaveChangesAsync();
        var repository = new AttachmentRepository(db);

        // Act
        var attachments = await repository.GetByIssueIdAsync(10);

        // Assert
        Assert.Empty(attachments);
    }

    private static JiraCloneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<JiraCloneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new JiraCloneDbContext(options);
    }

    private static void SeedUser(JiraCloneDbContext db)
    {
        db.Users.Add(new User { Id = 1, UserName = "bob", DisplayName = "Bob", Email = "bob@example.com", PasswordHash = "h", PasswordSalt = "s" });
        db.SaveChanges();
    }
}
