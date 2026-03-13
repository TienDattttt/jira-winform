using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLogs");
        builder.HasIndex(x => new { x.ProjectId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.IssueId, x.OccurredAtUtc });
        builder.HasIndex(x => x.UserId);
        builder.Property(x => x.FieldName).HasMaxLength(150);
        builder.Property(x => x.OldValue).HasMaxLength(2000);
        builder.Property(x => x.NewValue).HasMaxLength(2000);
    }
}
