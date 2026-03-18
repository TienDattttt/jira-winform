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
        await using var db = CreateContext();
        SeedLookupGraph(db);
        var reporter = db.Users.Single();
        var project = db.Projects.Single();
        var status = db.WorkflowStatuses.Single();
        db.Issues.Add(new Issue { Id = 10, ProjectId = project.Id, Project = project, IssueKey = "JIRA-1", Title = "Issue", ReporterId = reporter.Id, Reporter = reporter, CreatedById = reporter.Id, WorkflowStatus = status });
        await db.SaveChangesAsync();
        var repository = new IssueRepository(db);

        var issue = await repository.GetByIdAsync(10);

        Assert.NotNull(issue);
        Assert.NotNull(issue!.Reporter);
        Assert.NotNull(issue.WorkflowStatus);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        await using var db = CreateContext();
        var repository = new IssueRepository(db);

        var issue = await repository.GetByIdAsync(999);

        Assert.Null(issue);
    }

    [Fact]
    public async Task GetProjectIssuesAsync_ReturnsOnlyProjectIssues()
    {
        await using var db = CreateContext();
        SeedLookupGraph(db);
        var reporter = db.Users.Single();
        var status = db.WorkflowStatuses.Single();
        var projectOne = db.Projects.Single();
        var projectTwo = new Project { Id = 2, Key = "OPS", Name = "Ops" };
        db.Projects.Add(projectTwo);
        db.Issues.AddRange(
            new Issue { ProjectId = 1, Project = projectOne, IssueKey = "JIRA-1", Title = "P1", ReporterId = reporter.Id, Reporter = reporter, CreatedById = reporter.Id, WorkflowStatus = status },
            new Issue { ProjectId = 2, Project = projectTwo, IssueKey = "OPS-1", Title = "P2", ReporterId = reporter.Id, Reporter = reporter, CreatedById = reporter.Id, WorkflowStatus = status });
        await db.SaveChangesAsync();
        var repository = new IssueRepository(db);

        var issues = await repository.GetProjectIssuesAsync(1);

        Assert.Single(issues);
        Assert.Equal(1, issues[0].ProjectId);
    }

    [Fact]
    public async Task GetProjectIssuesAsync_SoftDeletedIssue_DoesNotAppear()
    {
        await using var db = CreateContext();
        SeedLookupGraph(db);
        var reporter = db.Users.Single();
        var project = db.Projects.Single();
        var status = db.WorkflowStatuses.Single();
        db.Issues.Add(new Issue { ProjectId = 1, Project = project, IssueKey = "JIRA-1", Title = "Hidden", ReporterId = reporter.Id, Reporter = reporter, CreatedById = reporter.Id, WorkflowStatus = status, IsDeleted = true });
        await db.SaveChangesAsync();
        var repository = new IssueRepository(db);

        var issues = await repository.GetProjectIssuesAsync(1);

        Assert.Empty(issues);
    }

    [Fact]
    public async Task AddAsync_ThenSave_IssueAppearsInSubsequentQuery()
    {
        await using var db = CreateContext();
        SeedLookupGraph(db);
        var reporter = db.Users.Single();
        var project = db.Projects.Single();
        var status = db.WorkflowStatuses.Single();
        var repository = new IssueRepository(db);
        var issue = new Issue { ProjectId = 1, Project = project, IssueKey = "JIRA-1", Title = "Created", ReporterId = reporter.Id, Reporter = reporter, CreatedById = reporter.Id, WorkflowStatus = status };

        await repository.AddAsync(issue);
        await db.SaveChangesAsync();
        var loaded = await repository.GetProjectIssuesAsync(1);

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

    private static void SeedLookupGraph(JiraCloneDbContext db)
    {
        var reporter = new User { Id = 1, UserName = "admin", DisplayName = "Admin", Email = "a@a.com", PasswordHash = "h", PasswordSalt = "s" };
        var project = new Project { Id = 1, Key = "JIRA", Name = "Jira" };
        var workflow = new WorkflowDefinition { Id = 1, ProjectId = 1, Project = project, Name = "Default", IsDefault = true };
        var status = new WorkflowStatus { Id = 1, WorkflowDefinitionId = 1, WorkflowDefinition = workflow, Name = "Backlog", Category = StatusCategory.ToDo, Color = "#42526E", DisplayOrder = 1 };
        workflow.Statuses.Add(status);
        project.WorkflowDefinitions.Add(workflow);
        db.Users.Add(reporter);
        db.Projects.Add(project);
        db.WorkflowDefinitions.Add(workflow);
        db.WorkflowStatuses.Add(status);
        db.SaveChanges();
    }
}
