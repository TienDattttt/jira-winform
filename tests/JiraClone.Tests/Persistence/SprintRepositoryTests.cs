using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.Persistence;
using JiraClone.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Tests.Persistence;

public class SprintRepositoryTests
{
    [Fact]
    public async Task GetByProjectIdAsync_MultipleSprints_ReturnsProjectSprintsOrderedByStartDateDescending()
    {
        // Arrange
        await using var db = CreateContext();
        db.Sprints.AddRange(
            new Sprint { Id = 1, ProjectId = 1, Name = "Older", State = SprintState.Closed, StartDate = new DateOnly(2026, 3, 1) },
            new Sprint { Id = 2, ProjectId = 1, Name = "Newer", State = SprintState.Planned, StartDate = new DateOnly(2026, 3, 10) },
            new Sprint { Id = 3, ProjectId = 2, Name = "Other", State = SprintState.Active, StartDate = new DateOnly(2026, 3, 5) });
        await db.SaveChangesAsync();
        var repository = new SprintRepository(db);

        // Act
        var sprints = await repository.GetByProjectIdAsync(1);

        // Assert
        Assert.Equal(2, sprints.Count);
        Assert.Equal("Newer", sprints[0].Name);
        Assert.Equal("Older", sprints[1].Name);
    }

    [Fact]
    public async Task GetActiveByProjectIdAsync_ActiveSprintExists_ReturnsProjectActiveSprint()
    {
        // Arrange
        await using var db = CreateContext();
        db.Sprints.AddRange(
            new Sprint { Id = 1, ProjectId = 1, Name = "Planned", State = SprintState.Planned },
            new Sprint { Id = 2, ProjectId = 1, Name = "Active", State = SprintState.Active, StartDate = new DateOnly(2026, 3, 10) },
            new Sprint { Id = 3, ProjectId = 2, Name = "Other", State = SprintState.Active, StartDate = new DateOnly(2026, 3, 11) });
        await db.SaveChangesAsync();
        var repository = new SprintRepository(db);

        // Act
        var sprint = await repository.GetActiveByProjectIdAsync(1);

        // Assert
        Assert.NotNull(sprint);
        Assert.Equal(2, sprint!.Id);
    }

    private static JiraCloneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<JiraCloneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new JiraCloneDbContext(options);
    }
}
