using System.Text.RegularExpressions;
using JiraClone.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JiraClone.Tests.Integration;

public class MigrationScriptTests
{
    [Fact]
    public void GenerateScript_AllMigrations_ContainsExpectedObjects()
    {
        // Arrange
        using var db = CreateDbContext();
        var migrator = db.GetService<IMigrator>();

        // Act
        var script = migrator.GenerateScript();

        // Assert
        Assert.Contains("CREATE TABLE [Projects]", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE [Issues]", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ALTER TABLE [Issues] ADD [IsDeleted]", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateScript_AllMigrations_DoesNotContainUpdateWithoutSetClause()
    {
        // Arrange
        using var db = CreateDbContext();
        var migrator = db.GetService<IMigrator>();

        // Act
        var script = migrator.GenerateScript();

        // Assert
        foreach (var statement in SplitStatements(script).Where(x => x.StartsWith("UPDATE ", StringComparison.OrdinalIgnoreCase)))
        {
            Assert.Contains(" SET ", statement, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static JiraCloneDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<JiraCloneDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=JiraClone_MigrationScriptTests;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new JiraCloneDbContext(options);
    }

    private static IReadOnlyList<string> SplitStatements(string script)
    {
        return Regex.Split(script, @"^GO\s*$", RegexOptions.Multiline)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }
}

