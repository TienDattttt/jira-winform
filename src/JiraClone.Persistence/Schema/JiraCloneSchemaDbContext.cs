using System.Text.RegularExpressions;
using JiraClone.Persistence.Schema.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace JiraClone.Persistence.Schema;

public class JiraCloneSchemaDbContext : DbContext
{
    public JiraCloneSchemaDbContext(DbContextOptions<JiraCloneSchemaDbContext> options) : base(options)
    {
    }

    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<ProjectMemberEntity> ProjectMembers => Set<ProjectMemberEntity>();
    public DbSet<IssueTypeEntity> IssueTypes => Set<IssueTypeEntity>();
    public DbSet<IssueStatusEntity> IssueStatuses => Set<IssueStatusEntity>();
    public DbSet<PriorityEntity> Priorities => Set<PriorityEntity>();
    public DbSet<BoardColumnEntity> BoardColumns => Set<BoardColumnEntity>();
    public DbSet<SprintEntity> Sprints => Set<SprintEntity>();
    public DbSet<IssueEntity> Issues => Set<IssueEntity>();
    public DbSet<IssueAssigneeEntity> IssueAssignees => Set<IssueAssigneeEntity>();
    public DbSet<CommentEntity> Comments => Set<CommentEntity>();
    public DbSet<AttachmentEntity> Attachments => Set<AttachmentEntity>();
    public DbSet<ActivityLogEntity> ActivityLogs => Set<ActivityLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureTables(modelBuilder);
        ConfigureRelationships(modelBuilder);
        ConfigureIndexes(modelBuilder);
        ConfigureSeedData(modelBuilder);
        ApplySnakeCaseColumns(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoleEntity>().ToTable("Roles");
        modelBuilder.Entity<UserEntity>().ToTable("Users");
        modelBuilder.Entity<UserRoleEntity>().ToTable("UserRoles");
        modelBuilder.Entity<ProjectEntity>().ToTable("Projects");
        modelBuilder.Entity<ProjectMemberEntity>().ToTable("ProjectMembers");
        modelBuilder.Entity<IssueTypeEntity>().ToTable("IssueTypes");
        modelBuilder.Entity<IssueStatusEntity>().ToTable("IssueStatuses");
        modelBuilder.Entity<PriorityEntity>().ToTable("Priorities");
        modelBuilder.Entity<BoardColumnEntity>().ToTable("BoardColumns");
        modelBuilder.Entity<SprintEntity>().ToTable("Sprints");
        modelBuilder.Entity<IssueEntity>().ToTable("Issues");
        modelBuilder.Entity<IssueAssigneeEntity>().ToTable("IssueAssignees");
        modelBuilder.Entity<CommentEntity>().ToTable("Comments");
        modelBuilder.Entity<AttachmentEntity>().ToTable("Attachments");
        modelBuilder.Entity<ActivityLogEntity>().ToTable("ActivityLogs");

        modelBuilder.Entity<IssueEntity>().Property(x => x.BoardPosition).HasColumnType("decimal(18,4)");
        modelBuilder.Entity<IssueEntity>().Property(x => x.RowVersion).IsRowVersion();
        modelBuilder.Entity<CommentEntity>().Property(x => x.RowVersion).IsRowVersion();
    }

    private static void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRoleEntity>()
            .HasOne(x => x.User)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserRoleEntity>()
            .HasOne(x => x.Role)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProjectMemberEntity>()
            .HasOne(x => x.Project)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProjectMemberEntity>()
            .HasOne(x => x.User)
            .WithMany(x => x.ProjectMembers)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BoardColumnEntity>()
            .HasOne(x => x.Project)
            .WithMany(x => x.BoardColumns)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BoardColumnEntity>()
            .HasOne(x => x.IssueStatus)
            .WithMany()
            .HasForeignKey(x => x.IssueStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SprintEntity>()
            .HasOne(x => x.Project)
            .WithMany(x => x.Sprints)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IssueEntity>()
            .HasOne(x => x.Project)
            .WithMany(x => x.Issues)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IssueEntity>()
            .HasOne(x => x.Sprint)
            .WithMany(x => x.Issues)
            .HasForeignKey(x => x.SprintId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IssueEntity>()
            .HasOne(x => x.IssueType)
            .WithMany()
            .HasForeignKey(x => x.IssueTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IssueEntity>()
            .HasOne(x => x.IssueStatus)
            .WithMany()
            .HasForeignKey(x => x.IssueStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IssueEntity>()
            .HasOne(x => x.Priority)
            .WithMany()
            .HasForeignKey(x => x.PriorityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IssueEntity>()
            .HasOne(x => x.Reporter)
            .WithMany()
            .HasForeignKey(x => x.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IssueEntity>()
            .HasOne(x => x.CreatedBy)
            .WithMany()
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IssueAssigneeEntity>()
            .HasOne(x => x.Issue)
            .WithMany(x => x.Assignees)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IssueAssigneeEntity>()
            .HasOne(x => x.User)
            .WithMany(x => x.IssueAssignees)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CommentEntity>()
            .HasOne(x => x.Issue)
            .WithMany(x => x.Comments)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CommentEntity>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AttachmentEntity>()
            .HasOne(x => x.Issue)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AttachmentEntity>()
            .HasOne(x => x.UploadedBy)
            .WithMany()
            .HasForeignKey(x => x.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityLogEntity>()
            .HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityLogEntity>()
            .HasOne(x => x.Issue)
            .WithMany(x => x.ActivityLogs)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityLogEntity>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoleEntity>().HasIndex(x => x.Name).IsUnique().HasFilter("[is_deleted] = 0");
        modelBuilder.Entity<UserEntity>().HasIndex(x => x.UserName).IsUnique().HasFilter("[is_deleted] = 0");
        modelBuilder.Entity<UserEntity>().HasIndex(x => x.Email).IsUnique().HasFilter("[is_deleted] = 0");
        modelBuilder.Entity<UserRoleEntity>().HasIndex(x => new { x.UserId, x.RoleId }).IsUnique().HasFilter("[is_deleted] = 0");
        modelBuilder.Entity<ProjectEntity>().HasIndex(x => x.ProjectKey).IsUnique().HasFilter("[is_deleted] = 0");
        modelBuilder.Entity<ProjectMemberEntity>().HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique().HasFilter("[is_deleted] = 0");
        modelBuilder.Entity<ProjectMemberEntity>().HasIndex(x => x.UserId);
        modelBuilder.Entity<ProjectMemberEntity>().HasIndex(x => new { x.ProjectId, x.ProjectRole });
        modelBuilder.Entity<IssueTypeEntity>().HasIndex(x => x.Code).IsUnique().HasFilter("[is_deleted] = 0");
        modelBuilder.Entity<IssueStatusEntity>().HasIndex(x => x.Code).IsUnique().HasFilter("[is_deleted] = 0");
        modelBuilder.Entity<PriorityEntity>().HasIndex(x => x.Code).IsUnique().HasFilter("[is_deleted] = 0");
        modelBuilder.Entity<BoardColumnEntity>().HasIndex(x => new { x.ProjectId, x.IssueStatusId }).IsUnique().HasFilter("[is_deleted] = 0");
        modelBuilder.Entity<BoardColumnEntity>().HasIndex(x => new { x.ProjectId, x.DisplayOrder });
        modelBuilder.Entity<SprintEntity>().HasIndex(x => new { x.ProjectId, x.SprintState });
        modelBuilder.Entity<SprintEntity>().HasIndex(x => new { x.ProjectId, x.StartDate });
        modelBuilder.Entity<IssueEntity>().HasIndex(x => new { x.ProjectId, x.IssueKey }).IsUnique().HasFilter("[is_deleted] = 0");
        modelBuilder.Entity<IssueEntity>().HasIndex(x => new { x.ProjectId, x.IssueStatusId, x.BoardPosition });
        modelBuilder.Entity<IssueEntity>().HasIndex(x => new { x.ProjectId, x.SprintId, x.IssueStatusId });
        modelBuilder.Entity<IssueEntity>().HasIndex(x => x.ReporterId);
        modelBuilder.Entity<IssueEntity>().HasIndex(x => x.UpdatedAt);
        modelBuilder.Entity<IssueAssigneeEntity>().HasIndex(x => new { x.IssueId, x.UserId }).IsUnique().HasFilter("[is_deleted] = 0");
        modelBuilder.Entity<IssueAssigneeEntity>().HasIndex(x => x.UserId);
        modelBuilder.Entity<IssueAssigneeEntity>().HasIndex(x => new { x.IssueId, x.AssignedAt });
        modelBuilder.Entity<CommentEntity>().HasIndex(x => new { x.IssueId, x.CreatedAt });
        modelBuilder.Entity<CommentEntity>().HasIndex(x => x.UserId);
        modelBuilder.Entity<AttachmentEntity>().HasIndex(x => new { x.IssueId, x.UploadedAt });
        modelBuilder.Entity<AttachmentEntity>().HasIndex(x => x.UploadedById);
        modelBuilder.Entity<AttachmentEntity>().HasIndex(x => x.ChecksumSha256);
        modelBuilder.Entity<ActivityLogEntity>().HasIndex(x => new { x.ProjectId, x.OccurredAt });
        modelBuilder.Entity<ActivityLogEntity>().HasIndex(x => new { x.IssueId, x.OccurredAt });
        modelBuilder.Entity<ActivityLogEntity>().HasIndex(x => x.UserId);
    }

    private static void ConfigureSeedData(ModelBuilder modelBuilder)
    {
        var seedTime = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<RoleEntity>().HasData(
            new RoleEntity { Id = 1, Name = "Admin", Description = "Full system access", CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false },
            new RoleEntity { Id = 2, Name = "ProjectManager", Description = "Project administration access", CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false },
            new RoleEntity { Id = 3, Name = "Developer", Description = "Issue editing access", CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false },
            new RoleEntity { Id = 4, Name = "Viewer", Description = "Read-only access", CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false });

        modelBuilder.Entity<IssueTypeEntity>().HasData(
            new IssueTypeEntity { Id = 1, Code = "task", Name = "Task", Description = "A task represents work that needs to be done.", SortOrder = 1, CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false },
            new IssueTypeEntity { Id = 2, Code = "bug", Name = "Bug", Description = "A bug represents a defect or malfunction.", SortOrder = 2, CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false },
            new IssueTypeEntity { Id = 3, Code = "story", Name = "Story", Description = "A story represents user-facing functionality.", SortOrder = 3, CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false });

        modelBuilder.Entity<IssueStatusEntity>().HasData(
            new IssueStatusEntity { Id = 1, Code = "backlog", Name = "Backlog", StatusCategory = "todo", SortOrder = 1, CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false },
            new IssueStatusEntity { Id = 2, Code = "selected", Name = "Selected", StatusCategory = "todo", SortOrder = 2, CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false },
            new IssueStatusEntity { Id = 3, Code = "in_progress", Name = "In Progress", StatusCategory = "active", SortOrder = 3, CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false },
            new IssueStatusEntity { Id = 4, Code = "done", Name = "Done", StatusCategory = "done", SortOrder = 4, CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false });

        modelBuilder.Entity<PriorityEntity>().HasData(
            new PriorityEntity { Id = 1, Code = "lowest", Name = "Lowest", Weight = 1, ColorHex = "#66B966", SortOrder = 1, CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false },
            new PriorityEntity { Id = 2, Code = "low", Name = "Low", Weight = 2, ColorHex = "#008A00", SortOrder = 2, CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false },
            new PriorityEntity { Id = 3, Code = "medium", Name = "Medium", Weight = 3, ColorHex = "#FF9900", SortOrder = 3, CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false },
            new PriorityEntity { Id = 4, Code = "high", Name = "High", Weight = 4, ColorHex = "#F06666", SortOrder = 4, CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false },
            new PriorityEntity { Id = 5, Code = "highest", Name = "Highest", Weight = 5, ColorHex = "#E60000", SortOrder = 5, CreatedAt = seedTime, UpdatedAt = seedTime, IsDeleted = false });
    }

    private static void ApplySnakeCaseColumns(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName()!));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName()!));
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var normalized = Regex.Replace(value, "([a-z0-9])([A-Z])", "$1_$2");
        normalized = Regex.Replace(normalized, "([A-Z])([A-Z][a-z])", "$1_$2");
        return normalized.ToLowerInvariant();
    }
}
