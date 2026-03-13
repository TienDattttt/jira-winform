using JiraClone.Domain.Entities;
using JiraClone.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace JiraClone.Tests.Integration;

public class DbContextConcurrencyTests
{
    [Fact]
    public async Task CreateDbContext_ParallelCalls_UseSeparateInstancesWithoutConcurrencyError()
    {
        // Arrange
        var factory = CreateFactory();
        await SeedAsync(factory);

        // Act
        var first = Task.Run(async () =>
        {
            await using var db = await factory.CreateDbContextAsync();
            return await db.Users.CountAsync();
        });
        var second = Task.Run(async () =>
        {
            await using var db = await factory.CreateDbContextAsync();
            return await db.Users.CountAsync();
        });
        var results = await Task.WhenAll(first, second);

        // Assert
        Assert.Equal(new[] { 1, 1 }, results);
    }

    [Fact]
    public void CreateDbContext_DisposedInstance_ThrowsObjectDisposedException()
    {
        // Arrange
        var factory = CreateFactory();
        JiraCloneDbContext db;
        using (db = factory.CreateDbContext())
        {
        }

        // Act
        Action act = () => _ = db.Users.Any();

        // Assert
        Assert.Throws<ObjectDisposedException>(act);
    }

    private static IDbContextFactory<JiraCloneDbContext> CreateFactory()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<JiraCloneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), databaseRoot)
            .Options;

        return new TestDbContextFactory(options);
    }

    private static async Task SeedAsync(IDbContextFactory<JiraCloneDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Users.Add(new User { Id = 1, UserName = "admin", DisplayName = "Admin", Email = "admin@example.com", PasswordHash = "h", PasswordSalt = "s" });
        await db.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<JiraCloneDbContext>
    {
        private readonly DbContextOptions<JiraCloneDbContext> _options;

        public TestDbContextFactory(DbContextOptions<JiraCloneDbContext> options)
        {
            _options = options;
        }

        public JiraCloneDbContext CreateDbContext() => new(_options);

        public Task<JiraCloneDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
