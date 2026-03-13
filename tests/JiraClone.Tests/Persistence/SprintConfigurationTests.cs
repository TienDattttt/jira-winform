using JiraClone.Domain.Entities;
using JiraClone.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace JiraClone.Tests.Persistence;

public class SprintConfigurationTests
{
    [Fact]
    public void Model_SprintActiveIndex_IsUniqueAndFiltered()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<JiraCloneDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=JiraCloneModelOnly;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        using var context = new JiraCloneDbContext(options);

        // Act
        var sprintEntity = context.Model.FindEntityType(typeof(Sprint));
        var activeIndex = sprintEntity!.GetIndexes().FirstOrDefault(x =>
            x.Properties.Count == 1 &&
            x.Properties[0].Name == nameof(Sprint.ProjectId) &&
            x.IsUnique &&
            x.GetFilter() == "[State] = 2");

        // Assert
        Assert.NotNull(activeIndex);
    }
}
