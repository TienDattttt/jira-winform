using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence;

public class JiraCloneDbContext : DbContext
{
    public JiraCloneDbContext(DbContextOptions<JiraCloneDbContext> options) : base(options)
    {
    }

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();
    public DbSet<ApiTokenScopeGrant> ApiTokenScopeGrants => Set<ApiTokenScopeGrant>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Component> Components => Set<Component>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueAssignee> IssueAssignees => Set<IssueAssignee>();
    public DbSet<IssueComponent> IssueComponents => Set<IssueComponent>();
    public DbSet<IssueLabel> IssueLabels => Set<IssueLabel>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectIntegrationConfig> ProjectIntegrationConfigs => Set<ProjectIntegrationConfig>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectVersion> ProjectVersions => Set<ProjectVersion>();
    public DbSet<SavedFilter> SavedFilters => Set<SavedFilter>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStatus> WorkflowStatuses => Set<WorkflowStatus>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();
    public DbSet<PermissionScheme> PermissionSchemes => Set<PermissionScheme>();
    public DbSet<Watcher> Watchers => Set<Watcher>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<WebhookEndpointSubscription> WebhookEndpointSubscriptions => Set<WebhookEndpointSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JiraCloneDbContext).Assembly);
        Seed.SeedData.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
