using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence;

public class JiraCloneDbContext : DbContext
{
    public JiraCloneDbContext(DbContextOptions<JiraCloneDbContext> options) : base(options)
    {
    }

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueAssignee> IssueAssignees => Set<IssueAssignee>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JiraCloneDbContext).Assembly);
        Seed.SeedData.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
