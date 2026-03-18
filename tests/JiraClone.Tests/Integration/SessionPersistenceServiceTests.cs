using JiraClone.Application.Models;
using JiraClone.Infrastructure.Session;

namespace JiraClone.Tests.Integration;

public class SessionPersistenceServiceTests
{
    [Fact]
    public async Task SaveLoadAndClearAsync_RoundTripsEncryptedSession()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var rootPath = Path.Combine(Path.GetTempPath(), "JiraClone.SessionTests", Guid.NewGuid().ToString("N"));
        var sessionPath = Path.Combine(rootPath, "session.dat");
        var service = new DpapiSessionPersistenceService(new SessionPersistenceOptions { SessionFilePath = sessionPath });
        var session = new SessionData
        {
            UserId = 7,
            Username = "gaben",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            RefreshToken = "token-123"
        };

        try
        {
            await service.SaveAsync(session);
            Assert.True(File.Exists(sessionPath));

            var loaded = await service.LoadAsync();

            Assert.NotNull(loaded);
            Assert.Equal(session.UserId, loaded!.UserId);
            Assert.Equal(session.Username, loaded.Username);
            Assert.Equal(session.RefreshToken, loaded.RefreshToken);

            await service.ClearAsync();
            Assert.False(File.Exists(sessionPath));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_CorruptedFile_ClearsAndReturnsNull()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var rootPath = Path.Combine(Path.GetTempPath(), "JiraClone.SessionTests", Guid.NewGuid().ToString("N"));
        var sessionPath = Path.Combine(rootPath, "session.dat");
        Directory.CreateDirectory(rootPath);
        await File.WriteAllTextAsync(sessionPath, "not-encrypted-json");
        var service = new DpapiSessionPersistenceService(new SessionPersistenceOptions { SessionFilePath = sessionPath });

        try
        {
            var loaded = await service.LoadAsync();

            Assert.Null(loaded);
            Assert.False(File.Exists(sessionPath));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }
}
