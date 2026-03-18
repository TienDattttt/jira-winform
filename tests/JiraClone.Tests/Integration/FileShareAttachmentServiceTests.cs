using JiraClone.Infrastructure.Storage;

namespace JiraClone.Tests.Integration;

public class FileShareAttachmentServiceTests
{
    [Fact]
    public async Task SaveAsync_TextFile_SetsTextPlainContentType()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), $"jira-attachments-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var sourceFile = Path.Combine(root, "note.txt");
        await File.WriteAllTextAsync(sourceFile, "hello world");
        var service = new FileShareAttachmentService(root, 10 * 1024 * 1024);

        try
        {
            // Act
            var attachment = await service.SaveAsync(5, 9, sourceFile);

            // Assert
            Assert.Equal("text/plain", attachment.ContentType);
            Assert.True(File.Exists(attachment.StoragePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
