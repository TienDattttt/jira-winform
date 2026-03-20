using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("Issues");
        builder.HasIndex(x => new { x.ProjectId, x.IssueKey }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.WorkflowStatusId, x.BoardPosition });
        builder.HasIndex(x => new { x.ProjectId, x.SprintId, x.WorkflowStatusId });
        builder.HasIndex(x => x.ReporterId);
        builder.HasIndex(x => x.UpdatedAtUtc);
        builder.HasIndex(x => x.ParentIssueId);
        builder.HasIndex(x => x.FixVersionId);
        builder.HasIndex(x => new { x.ProjectId, x.StartDate, x.DueDate });
        builder.Property(x => x.IssueKey).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BoardPosition).HasColumnType("decimal(18,4)");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ParentIssue).WithMany(x => x.SubIssues).HasForeignKey(x => x.ParentIssueId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FixVersion).WithMany(x => x.Issues).HasForeignKey(x => x.FixVersionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.WorkflowStatus).WithMany(x => x.Issues).HasForeignKey(x => x.WorkflowStatusId).OnDelete(DeleteBehavior.Restrict);
    }
}
